namespace KnowledgeVault.Infrastructure.AI;

public sealed record VectorSearchResult(
    string Id,
    VectorSourceType Source,
    string SourceId,
    Guid? ProjectId,
    Guid? OwnerUserId,
    string Text,
    IReadOnlyDictionary<string, string> Metadata,
    double Score);
