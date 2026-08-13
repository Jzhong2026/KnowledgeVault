namespace KnowledgeVault.Infrastructure.AI;

public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// Thin abstraction over an OpenAI-compatible chat-completions endpoint.
/// Implementations must support streaming via SSE; they must NOT throw on
/// mid-stream cancellation.
/// </summary>
public interface ILLMProvider
{
    /// <summary>Non-streaming completion. Returns the assistant's full text.</summary>
    Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature = null,
        CancellationToken cancellationToken = default);

    /// <summary>Streaming completion. Yields text fragments as they arrive.</summary>
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature = null,
        CancellationToken cancellationToken = default);
}
