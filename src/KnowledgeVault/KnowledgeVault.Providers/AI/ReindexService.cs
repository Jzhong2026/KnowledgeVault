using System.Security.Cryptography;
using System.Text;
using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.AI;
using KnowledgeVault.Infrastructure.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Full-reindex implementation. Walks every visible KnowledgeItem, its
/// revisions, reviews, comments, and accepted memory candidates; chunks
/// each Markdown body, embeds the chunks, and upserts them to the vector
/// store. Each chunk carries a deterministic id derived from
/// (source, sourceId, order) so a re-run is idempotent.
/// </summary>
public sealed class ReindexService : IReindexService
{
    private readonly KnowledgeVaultDbContext _db;
    private readonly IEmbeddingProvider _embedder;
    private readonly IVectorStore _vector;
    private readonly MarkdownChunker _chunker;
    private readonly ILogger<ReindexService> _logger;

    private static ReindexStatus _current = new(false, null, 0, null);
    private static readonly object _statusLock = new();

    public ReindexService(
        KnowledgeVaultDbContext db,
        IEmbeddingProvider embedder,
        IVectorStore vector,
        MarkdownChunker chunker,
        ILogger<ReindexService> logger)
    {
        _db = db;
        _embedder = embedder;
        _vector = vector;
        _chunker = chunker;
        _logger = logger;
    }

    public Task<ReindexStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (_statusLock) return Task.FromResult(_current);
    }

    public async Task<ReindexStatus> ReindexAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_statusLock)
        {
            if (_current.IsRunning) return _current;
            _current = _current with { IsRunning = true, LastError = null };
        }
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            await _vector.EnsureCollectionAsync(cancellationToken);
            await _vector.DeleteAllAsync(cancellationToken);

            var totalChunks = 0;
            totalChunks += await ReindexKnowledgeItemsAsync(cancellationToken);
            totalChunks += await ReindexRevisionsAsync(cancellationToken);
            totalChunks += await ReindexReviewsAsync(cancellationToken);
            totalChunks += await ReindexCommentsAsync(cancellationToken);
            totalChunks += await ReindexMemoryCandidatesAsync(cancellationToken);

            var status = new ReindexStatus(false, DateTimeOffset.UtcNow, totalChunks, null);
            lock (_statusLock) _current = status;
            _logger.LogInformation("Reindex completed in {Elapsed} with {Total} chunks.", DateTimeOffset.UtcNow - startedAt, totalChunks);
            return status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reindex failed.");
            var status = new ReindexStatus(false, DateTimeOffset.UtcNow, 0, ex.Message);
            lock (_statusLock) _current = status;
            return status;
        }
    }

    private async Task<int> ReindexKnowledgeItemsAsync(CancellationToken ct)
    {
        var items = await _db.KnowledgeItems
            .AsNoTracking()
            .Include(x => x.CurrentRevision)
            .Where(x => x.Status != KnowledgeItemStatus.Deleted)
            .ToListAsync(ct);
        var chunks = new List<VectorChunk>(items.Count);
        foreach (var item in items)
        {
            var content = item.CurrentRevision?.Content;
            if (string.IsNullOrWhiteSpace(content)) continue;
            var segments = _chunker.Chunk(content, $"/documents/{item.Id}");
            foreach (var seg in segments)
            {
                chunks.Add(new VectorChunk(
                    Id: ChunkId(VectorSourceType.Document, item.Id.ToString(), seg.Order),
                    Source: VectorSourceType.Document,
                    SourceId: item.Id.ToString(),
                    ProjectId: item.ProjectId,
                    OwnerUserId: item.ProjectId.HasValue ? null : item.OwnerUserId,
                    Scope: item.Scope.ToString(),
                    FolderId: item.FolderId,
                    DocumentType: item.DocumentType.ToString(),
                    RevisionNumber: item.CurrentRevisionNumber,
                    Status: item.Status.ToString(),
                    Text: seg.Text,
                    Embedding: Array.Empty<float>(),
                    ExtraMetadata: new Dictionary<string, string>
                    {
                        ["title"] = item.CurrentRevision?.Title ?? "Untitled",
                        ["anchor"] = seg.Anchor
                    }));
            }
        }
        await EmbedAndUpsertAsync(chunks, ct);
        return chunks.Count;
    }

    private async Task<int> ReindexRevisionsAsync(CancellationToken ct)
    {
        var revisions = await _db.KnowledgeItemRevisions
            .AsNoTracking()
            .Include(x => x.KnowledgeItem)
            .ToListAsync(ct);
        var chunks = new List<VectorChunk>(revisions.Count);
        foreach (var rev in revisions)
        {
            if (rev.KnowledgeItem.Status == KnowledgeItemStatus.Deleted) continue;
            if (string.IsNullOrWhiteSpace(rev.Content)) continue;
            var segments = _chunker.Chunk(rev.Content, $"/documents/{rev.KnowledgeItemId}#revision-{rev.RevisionNumber}");
            foreach (var seg in segments)
            {
                chunks.Add(new VectorChunk(
                    Id: ChunkId(VectorSourceType.Revision, rev.Id.ToString(), seg.Order),
                    Source: VectorSourceType.Revision,
                    SourceId: rev.Id.ToString(),
                    ProjectId: rev.KnowledgeItem.ProjectId,
                    OwnerUserId: rev.KnowledgeItem.ProjectId.HasValue ? null : rev.KnowledgeItem.OwnerUserId,
                    Scope: rev.KnowledgeItem.Scope.ToString(),
                    FolderId: rev.KnowledgeItem.FolderId,
                    DocumentType: rev.KnowledgeItem.DocumentType.ToString(),
                    RevisionNumber: rev.RevisionNumber,
                    Status: rev.KnowledgeItem.Status.ToString(),
                    Text: seg.Text,
                    Embedding: Array.Empty<float>(),
                    ExtraMetadata: new Dictionary<string, string>
                    {
                        ["title"] = rev.Title,
                        ["anchor"] = seg.Anchor
                    }));
            }
        }
        await EmbedAndUpsertAsync(chunks, ct);
        return chunks.Count;
    }

    private async Task<int> ReindexReviewsAsync(CancellationToken ct)
    {
        var reviews = await _db.DocumentRevisionReviews
            .AsNoTracking()
            .Include(x => x.Revision).ThenInclude(r => r.KnowledgeItem)
            .ToListAsync(ct);
        var chunks = new List<VectorChunk>(reviews.Count);
        foreach (var rev in reviews)
        {
            if (rev.Revision?.KnowledgeItem is null) continue;
            var text = new StringBuilder()
                .Append("Review status: ").AppendLine(rev.Status.ToString())
                .Append("Request message: ").AppendLine(rev.RequestMessage ?? "(none)")
                .Append("Decision comment: ").AppendLine(rev.DecisionComment ?? "(none)")
                .ToString();
            chunks.Add(new VectorChunk(
                Id: ChunkId(VectorSourceType.Review, rev.Id.ToString(), 0),
                Source: VectorSourceType.Review,
                SourceId: rev.Id.ToString(),
                ProjectId: rev.Revision.KnowledgeItem.ProjectId,
                OwnerUserId: null,
                Scope: rev.Revision.KnowledgeItem.Scope.ToString(),
                FolderId: null,
                DocumentType: null,
                RevisionNumber: rev.Revision.RevisionNumber,
                Status: rev.Status.ToString(),
                Text: text,
                Embedding: Array.Empty<float>(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["title"] = $"Review {rev.Id}",
                    ["anchor"] = $"/documents/{rev.Revision.KnowledgeItemId}#review-{rev.Id}"
                }));
        }
        await EmbedAndUpsertAsync(chunks, ct);
        return chunks.Count;
    }

    private async Task<int> ReindexCommentsAsync(CancellationToken ct)
    {
        var comments = await _db.KnowledgeItemComments
            .AsNoTracking()
            .Include(x => x.Revision).ThenInclude(r => r.KnowledgeItem)
            .ToListAsync(ct);
        var chunks = new List<VectorChunk>(comments.Count);
        foreach (var c in comments)
        {
            if (c.Revision?.KnowledgeItem is null) continue;
            chunks.Add(new VectorChunk(
                Id: ChunkId(VectorSourceType.Comment, c.Id.ToString(), 0),
                Source: VectorSourceType.Comment,
                SourceId: c.Id.ToString(),
                ProjectId: c.Revision.KnowledgeItem.ProjectId,
                OwnerUserId: null,
                Scope: c.Revision.KnowledgeItem.Scope.ToString(),
                FolderId: null,
                DocumentType: null,
                RevisionNumber: c.Revision.RevisionNumber,
                Status: null,
                Text: c.Content,
                Embedding: Array.Empty<float>(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["title"] = $"Comment by {c.AuthorUserId}",
                    ["anchor"] = $"/documents/{c.Revision.KnowledgeItemId}#comment-{c.Id}"
                }));
        }
        await EmbedAndUpsertAsync(chunks, ct);
        return chunks.Count;
    }

    private async Task<int> ReindexMemoryCandidatesAsync(CancellationToken ct)
    {
        // Index accepted candidates only — pending/rejected are not yet authoritative.
        var accepted = await _db.ProjectMemoryCandidates
            .AsNoTracking()
            .Where(x => x.Status == ProjectMemoryCandidateStatus.Accepted)
            .ToListAsync(ct);
        var chunks = new List<VectorChunk>(accepted.Count);
        foreach (var m in accepted)
        {
            chunks.Add(new VectorChunk(
                Id: ChunkId(VectorSourceType.MemoryAccepted, m.Id.ToString(), 0),
                Source: VectorSourceType.MemoryAccepted,
                SourceId: m.Id.ToString(),
                ProjectId: m.ProjectId,
                OwnerUserId: null,
                Scope: DocumentScope.Project.ToString(),
                FolderId: null,
                DocumentType: DocumentType.ProjectMemory.ToString(),
                RevisionNumber: null,
                Status: m.Status.ToString(),
                Text: m.ProposedContent,
                Embedding: Array.Empty<float>(),
                ExtraMetadata: new Dictionary<string, string>
                {
                    ["title"] = $"MEMORY.md § {m.TargetSection}",
                    ["anchor"] = $"/projects/{m.ProjectId}/memory#{m.TargetSection}"
                }));
        }
        await EmbedAndUpsertAsync(chunks, ct);
        return chunks.Count;
    }

    private async Task EmbedAndUpsertAsync(List<VectorChunk> chunks, CancellationToken ct)
    {
        if (chunks.Count == 0) return;
        const int batch = 32;
        for (var i = 0; i < chunks.Count; i += batch)
        {
            var slice = chunks.Skip(i).Take(batch).ToArray();
            var texts = slice.Select(c => c.Text).ToArray();
            var embeddings = await _embedder.EmbedBatchAsync(texts, ct);
            for (var j = 0; j < slice.Length; j++)
            {
                var replaced = slice[j] with { Embedding = embeddings[j] };
                chunks[i + j] = replaced;
            }
            await _vector.UpsertAsync(slice.Select((c, idx) => c with { Embedding = embeddings[idx] }).ToArray(), ct);
        }
    }

    private static string ChunkId(VectorSourceType source, string sourceId, int order)
    {
        var key = $"{source}:{sourceId}:{order}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..32].ToLowerInvariant();
    }
}
