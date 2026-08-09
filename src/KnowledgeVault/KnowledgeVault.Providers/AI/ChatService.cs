using System.Runtime.CompilerServices;
using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Infrastructure.AI;
using Microsoft.Extensions.Logging;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Coordinates the chatbot pipeline: classify intent, retrieve, build prompt,
/// call the LLM, extract citations, and stream results to the caller.
/// </summary>
public sealed class ChatService : IChatService
{
    private readonly IIntentRouter _router;
    private readonly IRetriever _retriever;
    private readonly ILLMProvider _llm;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IIntentRouter router,
        IRetriever retriever,
        ILLMProvider llm,
        ILogger<ChatService> logger)
    {
        _router = router;
        _retriever = retriever;
        _llm = llm;
        _logger = logger;
    }

    public async Task<ChatAnswer> AskAsync(
        ChatRequest request, RetrievalScope scope, CancellationToken cancellationToken = default)
    {
        var intent = await _router.ClassifyAsync(request.Message, cancellationToken);
        var retrieval = await _retriever.RetrieveAsync(intent, request.Message, scope, cancellationToken);
        var messages = ChatPromptBuilder.Build(request.Message, request.History ?? Array.Empty<ChatHistoryMessage>(), retrieval, intent);
        var answer = await _llm.CompleteAsync(messages, temperature: 0.2, cancellationToken: cancellationToken);
        var citations = ChatPromptBuilder.ExtractCitations(answer, retrieval);
        return new ChatAnswer(answer, citations);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request,
        RetrievalScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // The catch clause below CANNOT yield, so we do the work in a helper
        // and then yield events. Helper returns null on failure.
        var (intent, retrieval, error) = await PrepareAsync(request, scope, cancellationToken);
        if (error is not null)
        {
            yield return new ChatErrorEvent(error);
            yield break;
        }
        var messages = ChatPromptBuilder.Build(request.Message, request.History ?? Array.Empty<ChatHistoryMessage>(), retrieval!, intent);
        var fullText = new System.Text.StringBuilder();
        await foreach (var delta in _llm.StreamAsync(messages, temperature: 0.2, cancellationToken))
        {
            fullText.Append(delta);
            yield return new ChatTextEvent(delta);
        }
        var citations = ChatPromptBuilder.ExtractCitations(fullText.ToString(), retrieval!);
        yield return new ChatCitationsEvent(citations);
        yield return new ChatDoneEvent(fullText.ToString());
    }

    private async Task<(IntentKind Intent, IReadOnlyList<RetrievalResult>? Retrieval, string? Error)> PrepareAsync(
        ChatRequest request, RetrievalScope scope, CancellationToken ct)
    {
        try
        {
            var intent = await _router.ClassifyAsync(request.Message, ct);
            var retrieval = await _retriever.RetrieveAsync(intent, request.Message, scope, ct);
            return (intent, retrieval, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Intent/retrieval failed.");
            return (default, null, ex.Message);
        }
    }
}
