using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// OpenAI-compatible chat-completions client. The base URL is configurable so
/// this works against OpenAI, DeepSeek, vLLM, Ollama's OpenAI shim, etc.
/// </summary>
public sealed class OpenAiCompatibleLlmProvider : ILLMProvider
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleLlmProvider> _logger;
    private readonly ResiliencePipeline _pipeline;

    public OpenAiCompatibleLlmProvider(
        HttpClient http,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleLlmProvider> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(_options.ChatTimeoutSeconds, 30));
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
            })
            .Build();
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var req = BuildRequest(messages, _options.ChatModel, temperature, stream: false);
        using var resp = await _pipeline.ExecuteAsync(async ct =>
            await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct), cancellationToken);
        await EnsureSuccessAsync(resp, cancellationToken);
        var payload = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        return payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var req = BuildRequest(messages, _options.ChatModel, temperature, stream: true);
        using var resp = await _pipeline.ExecuteAsync(async ct =>
            await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct), cancellationToken);
        await EnsureSuccessAsync(resp, cancellationToken);
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) yield break;
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var data = line["data:".Length..].Trim();
            if (data == "[DONE]") yield break;
            string? delta = null;
            try
            {
                var chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data);
                delta = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM stream chunk: {Line}", data);
            }
            if (!string.IsNullOrEmpty(delta)) yield return delta;
        }
    }

    public async Task<string> CompleteWithModelAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        var req = BuildRequest(messages, model, temperature, stream: false);
        using var resp = await _pipeline.ExecuteAsync(async ct =>
            await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct), cancellationToken);
        await EnsureSuccessAsync(resp, cancellationToken);
        var payload = await resp.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        return payload?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
    }

    private static HttpRequestMessage BuildRequest(
        IReadOnlyList<ChatMessage> messages,
        string model,
        double? temperature,
        bool stream)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = temperature ?? 0.2,
            stream
        };
        var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        return req;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"LLM call failed ({(int)resp.StatusCode} {resp.StatusCode}): {body}");
    }

    // --- response DTOs (private) ---

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessageDto? Message { get; set; }
    }

    private sealed class ChatMessageDto
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ChatCompletionChunk
    {
        [JsonPropertyName("choices")]
        public List<ChatChunkChoice>? Choices { get; set; }
    }

    private sealed class ChatChunkChoice
    {
        [JsonPropertyName("delta")]
        public ChatDelta? Delta { get; set; }
    }

    private sealed class ChatDelta
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
