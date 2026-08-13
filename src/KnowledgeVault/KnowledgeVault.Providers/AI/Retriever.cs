using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.AI;
using Microsoft.Extensions.Logging;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// Default retriever. Strategy per intent:
///   - FindPlan: structured query against <see cref="IDocumentProvider"/>
///               filtering on DocumentType in (PlanningReview, TaskBreakdown)
///               and matching the user's free-text against title/summary.
///   - FindReview: structured query against <see cref="IDocumentReviewProvider"/>
///                 for project-visible reviews, then enrich with
///                 <see cref="ICommentProvider"/> for the most recent revision.
///   - FindMemory: pulls the live <see cref="IProjectMemoryProvider"/> result
///                 (no embedding needed; the memory is a single Markdown doc).
///   - GeneralQuestion: vector search with permission filters.
/// All paths pass a permission filter derived from the requesting user and
/// their accessible project set; the retriever never returns a chunk the
/// caller is not allowed to see.
/// </summary>
public sealed class Retriever : IRetriever
{
    private readonly IDocumentProvider _documents;
    private readonly IDocumentReviewProvider _reviews;
    private readonly ICommentProvider _comments;
    private readonly IProjectMemoryProvider _projectMemory;
    private readonly IEmbeddingProvider _embedder;
    private readonly IVectorStore _vector;
    private readonly ILogger<Retriever> _logger;

    public Retriever(
        IDocumentProvider documents,
        IDocumentReviewProvider reviews,
        ICommentProvider comments,
        IProjectMemoryProvider projectMemory,
        IEmbeddingProvider embedder,
        IVectorStore vector,
        ILogger<Retriever> logger)
    {
        _documents = documents;
        _reviews = reviews;
        _comments = comments;
        _projectMemory = projectMemory;
        _embedder = embedder;
        _vector = vector;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievalResult>> RetrieveAsync(
        IntentKind intent,
        string message,
        RetrievalScope scope,
        CancellationToken cancellationToken = default)
    {
        return intent switch
        {
            IntentKind.FindPlan => await FindPlanAsync(message, scope, cancellationToken),
            IntentKind.FindReview => await FindReviewAsync(message, scope, cancellationToken),
            IntentKind.FindMemory => await FindMemoryAsync(scope, cancellationToken),
            _ => await VectorSearchAsync(message, scope, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<RetrievalResult>> FindPlanAsync(
        string message, RetrievalScope scope, CancellationToken ct)
    {
        var result = new List<RetrievalResult>();
        var allowedScopes = new List<DocumentScope> { DocumentScope.Personal };
        if (scope.ProjectId.HasValue) allowedScopes.Add(DocumentScope.Project);

        foreach (var docScope in allowedScopes)
        {
            var query = new DocumentQuery(
                docScope,
                docScope == DocumentScope.Project ? scope.ProjectId : null,
                TopicId: null,
                DocumentType: null,
                LinkDisplayText: null,
                Search: message,
                CategoryId: null,
                OwnerUserId: docScope == DocumentScope.Personal ? scope.UserId : null,
                Status: null,
                TagIds: null,
                Sort: DocumentSort.UpdatedAtDesc,
                Page: 1,
                PageSize: 5,
                FolderId: null);
            try
            {
                var paged = await _documents.ListAsync(query, ct);
                foreach (var doc in paged.Items)
                {
                    if (!IsPlanDocument(doc)) continue;
                    result.Add(new RetrievalResult(
                        VectorSourceType.Document,
                        doc.Id.ToString(),
                        doc.Title,
                        $"/documents/{doc.Id}",
                        $"# {doc.Title}\n\n{doc.Summary}",
                        doc.ProjectId,
                        doc.ProjectId.HasValue ? null : scope.UserId,
                        0.0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FindPlan query failed for scope {Scope}.", docScope);
            }
        }
        return result;
    }

    private static bool IsPlanDocument(KnowledgeItemSummaryDto doc)
        => doc.DocumentType is DocumentType.PlanningReview or DocumentType.TaskBreakdown;

    private async Task<IReadOnlyList<RetrievalResult>> FindReviewAsync(
        string message, RetrievalScope scope, CancellationToken ct)
    {
        var result = new List<RetrievalResult>();
        if (!scope.ProjectId.HasValue)
        {
            // Reviews only exist for project-scope documents; the personal
            // workspace does not use them.
            return result;
        }
        try
        {
            var query = new DocumentReviewQuery(
                ProjectId: scope.ProjectId,
                DocumentId: null,
                Status: null,
                AssignedToMe: false,
                RequestedByMe: false,
                Page: 1,
                PageSize: 10);
            var paged = await _reviews.ListAsync(query, ct);
            foreach (var r in paged.Items.Take(5))
            {
                var text = $"Review {r.Id} ({r.Status}) by {r.ReviewerDisplayName} on document {r.DocumentId} rev {r.RevisionNumber}" +
                           (string.IsNullOrEmpty(r.DecisionComment) ? "" : $"\nDecision: {r.DecisionComment}");
                result.Add(new RetrievalResult(
                    VectorSourceType.Review,
                    r.Id.ToString(),
                    $"Document {r.DocumentId.ToString()[..8]}… rev {r.RevisionNumber} — {r.Status}",
                    $"/documents/{r.DocumentId}#revision-{r.RevisionNumber}",
                    text,
                    scope.ProjectId,
                    null,
                    0.0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FindReview query failed.");
        }
        return result;
    }

    private async Task<IReadOnlyList<RetrievalResult>> FindMemoryAsync(
        RetrievalScope scope, CancellationToken ct)
    {
        if (!scope.ProjectId.HasValue) return Array.Empty<RetrievalResult>();
        try
        {
            var memory = await _projectMemory.GetAsync(scope.ProjectId.Value, ct);
            if (memory is null) return Array.Empty<RetrievalResult>();
            return new[]
            {
                new RetrievalResult(
                    VectorSourceType.MemoryAccepted,
                    memory.Id.ToString(),
                    memory.Title,
                    $"/projects/{scope.ProjectId}/memory",
                    memory.Content ?? string.Empty,
                    scope.ProjectId,
                    null,
                    0.0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FindMemory failed for project {ProjectId}.", scope.ProjectId);
            return Array.Empty<RetrievalResult>();
        }
    }

    private async Task<IReadOnlyList<RetrievalResult>> VectorSearchAsync(
        string message, RetrievalScope scope, CancellationToken ct)
    {
        try
        {
            var embedding = await _embedder.EmbedAsync(message, ct);
            var query = new VectorQuery(
                embedding,
                TopK: scope.TopK,
                AllowedSources: null,
                AllowedProjectIds: scope.ProjectId.HasValue
                    ? new[] { scope.ProjectId.Value }
                    : scope.AllowedProjectIds,
                OwnerUserId: scope.ProjectId.HasValue ? null : scope.UserId);
            var hits = await _vector.SearchAsync(query, ct);
            return hits.Select(h => new RetrievalResult(
                h.Source, h.SourceId,
                Title: h.Metadata.GetValueOrDefault("title", h.SourceId),
                Anchor: h.Metadata.GetValueOrDefault("anchor", h.SourceId),
                Text: h.Text,
                ProjectId: h.ProjectId,
                OwnerUserId: h.OwnerUserId,
                Score: h.Score)).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector search failed; returning empty result set.");
            return Array.Empty<RetrievalResult>();
        }
    }
}
