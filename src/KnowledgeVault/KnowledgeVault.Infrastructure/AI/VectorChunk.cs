namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// A chunk of text together with its embedding and the metadata required to
/// reconstruct a citation and enforce permission filters on retrieval.
/// </summary>
public sealed record VectorChunk(
    string Id,
    VectorSourceType Source,
    string SourceId,
    Guid? ProjectId,
    Guid? OwnerUserId,
    string Scope,
    Guid? FolderId,
    string? DocumentType,
    int? RevisionNumber,
    string? Status,
    string Text,
    float[] Embedding,
    IReadOnlyDictionary<string, string> ExtraMetadata);
