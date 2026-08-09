using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Top-level retrieval interface. Permission filters must be enforced here
/// before any chunk reaches the LLM prompt. The retriever selects the
/// strategy (structured vs. vector) based on intent.
/// </summary>
public interface IRetriever
{
    Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        IntentKind intent,
        string message,
        RetrievalScope scope,
        CancellationToken cancellationToken = default);
}

public sealed record RetrievalScope(
    Guid UserId,
    Guid? ProjectId,
    IReadOnlyList<Guid> AllowedProjectIds,
    int TopK = 8);
