using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// OpenAI-compatible embeddings client. Defaults to text-embedding-3-small
/// (1536 dimensions). Supports batch embedding to minimize round-trips during
/// full reindex.
/// </summary>
public sealed class OpenAiCompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly ResiliencePipeline _pipeline;

    public int Dimensions => 1536;

    public OpenAiCompatibleEmbeddingProvider(HttpClient http, IOptions<LlmOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress ??= new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(_options.EmbeddingTimeoutSeconds, 30));
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

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var batch = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return batch[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0) return Array.Empty<float[]>();
        const int batchSize = 64;
        var result = new List<float[]>(texts.Count);
        for (var i = 0; i < texts.Count; i += batchSize)
        {
            var slice = texts.Skip(i).Take(batchSize).ToArray();
            var payload = new
            {
                model = _options.EmbeddingModel,
                input = slice
            };
            using var resp = await _pipeline.ExecuteAsync(async ct =>
                await _http.PostAsJsonAsync("embeddings", payload, ct), cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"Embedding call failed ({(int)resp.StatusCode} {resp.StatusCode}): {body}");
            }
            var parsed = await resp.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
            if (parsed?.Data is null) throw new HttpRequestException("Embedding response missing 'data'.");
            foreach (var d in parsed.Data)
            {
                result.Add(d.Embedding ?? Array.Empty<float>());
            }
        }
        return result;
    }

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("data")]
        public List<EmbeddingDatum>? Data { get; set; }
    }

    private sealed class EmbeddingDatum
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }
}
