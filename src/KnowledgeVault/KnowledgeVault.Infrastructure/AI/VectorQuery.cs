namespace KnowledgeVault.Infrastructure.AI;

public sealed record VectorQuery(
    float[] QueryEmbedding,
    int TopK = 8,
    IReadOnlyList<VectorSourceType>? AllowedSources = null,
    IReadOnlyList<Guid>? AllowedProjectIds = null,
    Guid? OwnerUserId = null,
    IReadOnlyDictionary<string, string>? Where = null);
