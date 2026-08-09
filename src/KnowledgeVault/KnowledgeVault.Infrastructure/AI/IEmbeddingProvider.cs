namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// Thin abstraction over an OpenAI-compatible embeddings endpoint. The
/// embedding dimensionality is the model's native size; the vector store
/// stores and queries vectors at the same dimensionality.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>Embedding dimensionality produced by this provider (e.g. 1536 for text-embedding-3-small).</summary>
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
