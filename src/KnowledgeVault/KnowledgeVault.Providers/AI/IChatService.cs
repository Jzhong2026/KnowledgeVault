using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Infrastructure.AI;

namespace KnowledgeVault.Providers.AI;

public interface IChatService
{
    /// <summary>Non-streaming answer + citations. Suitable for MCP tools and tests.</summary>
    Task<ChatAnswer> AskAsync(ChatRequest request, RetrievalScope scope, CancellationToken cancellationToken = default);

    /// <summary>Streaming answer, yielding text fragments then a final citation batch.</summary>
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, RetrievalScope scope, CancellationToken cancellationToken = default);
}

public abstract record ChatStreamEvent;

public sealed record ChatTextEvent(string Delta) : ChatStreamEvent;
public sealed record ChatCitationsEvent(IReadOnlyList<ChatCitation> Citations) : ChatStreamEvent;
public sealed record ChatDoneEvent(string FullText) : ChatStreamEvent;
public sealed record ChatErrorEvent(string Message) : ChatStreamEvent;
