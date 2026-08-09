namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// Vector store abstraction. The reference implementation targets ChromaDB's
/// HTTP API; tests can provide an in-memory implementation.
/// </summary>
public interface IVectorStore
{
    /// <summary>Idempotent — creates the collection if missing.</summary>
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Bulk-upsert chunks. Existing IDs are overwritten.</summary>
    Task UpsertAsync(
        IReadOnlyList<VectorChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>Delete every chunk belonging to a single source row (e.g. one document revision).</summary>
    Task DeleteBySourceAsync(
        VectorSourceType source,
        string sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>Delete EVERYTHING. Used by the full reindex entry point.</summary>
    Task DeleteAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Nearest-neighbor search with permission filters applied server-side.</summary>
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        VectorQuery query,
        CancellationToken cancellationToken = default);
}
