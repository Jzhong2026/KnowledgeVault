using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class DocumentReviewProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentAccessService documentAccessService,
    IDocumentProvider documentProvider,
    IRevisionProvider revisionProvider,
    ICommentProvider commentProvider) : IDocumentReviewProvider
{
    private const int MaxReviewers = 20;

    public async Task<IReadOnlyList<DocumentReviewDto>> CreateAsync(
        Guid documentId,
        CreateDocumentReviewRequest request,
        CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(documentId, cancellationToken);
        var userId = RequireCurrentUser();
        var reviewerIds = request.ReviewerUserIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray() ?? [];

        if (reviewerIds.Length == 0)
        {
            throw new ValidationException("At least one reviewer is required.");
        }

        if (reviewerIds.Length > MaxReviewers)
        {
            throw new ValidationException($"A review request can target at most {MaxReviewers} reviewers.");
        }

        var revision = await dbContext.KnowledgeItemRevisions
            .Include(x => x.KnowledgeItem)
            .FirstOrDefaultAsync(
                x => x.KnowledgeItemId == documentId && x.RevisionNumber == request.RevisionNumber,
                cancellationToken)
            ?? throw new NotFoundException("Revision was not found.");

        await ValidateReviewersAsync(revision.KnowledgeItem, reviewerIds, userId, cancellationToken);

        var existingReviewerIds = await dbContext.DocumentRevisionReviews
            .Where(x => x.KnowledgeItemRevisionId == revision.Id && reviewerIds.Contains(x.ReviewerUserId))
            .Select(x => x.ReviewerUserId)
            .ToListAsync(cancellationToken);
        if (existingReviewerIds.Count > 0)
        {
            throw new ConflictException(
                $"Review already exists for reviewer(s): {string.Join(", ", existingReviewerIds)}.");
        }

        var now = dateTimeProvider.UtcNow;
        var message = CleanOptional(request.Message, "Review message", 2000);
        var reviews = reviewerIds.Select(reviewerId => new DocumentRevisionReview
        {
            Id = Guid.NewGuid(),
            KnowledgeItemRevisionId = revision.Id,
            RequestedByUserId = userId,
            ReviewerUserId = reviewerId,
            Status = DocumentReviewStatus.Pending,
            RequestMessage = message,
            CreatedAt = now
        }).ToArray();

        dbContext.DocumentRevisionReviews.AddRange(reviews);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildReviewQuery()
            .Where(x => reviews.Select(review => review.Id).Contains(x.Id))
            .OrderBy(x => x.ReviewerUser!.UserName)
            .SelectReviewDtosAsync(cancellationToken);
    }

    public async Task<PagedResult<DocumentReviewDto>> ListAsync(
        DocumentReviewQuery query,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var reviews = BuildReviewQuery()
            .Where(x => x.Revision != null && x.Revision.KnowledgeItem != null)
            .Where(x =>
                (x.Revision!.KnowledgeItem!.Scope == DocumentScope.Personal &&
                 x.Revision.KnowledgeItem.OwnerUserId == userId) ||
                (x.Revision!.KnowledgeItem!.Scope == DocumentScope.Project &&
                 dbContext.ProjectMembers.Any(member =>
                     member.ProjectId == x.Revision.KnowledgeItem.ProjectId && member.UserId == userId)));

        if (query.ProjectId.HasValue)
        {
            reviews = reviews.Where(x => x.Revision!.KnowledgeItem!.ProjectId == query.ProjectId.Value);
        }

        if (query.DocumentId.HasValue)
        {
            reviews = reviews.Where(x => x.Revision!.KnowledgeItemId == query.DocumentId.Value);
        }

        if (query.Status.HasValue)
        {
            reviews = reviews.Where(x => x.Status == query.Status.Value);
        }

        if (query.AssignedToMe)
        {
            reviews = reviews.Where(x => x.ReviewerUserId == userId);
        }

        if (query.RequestedByMe)
        {
            reviews = reviews.Where(x => x.RequestedByUserId == userId);
        }

        var totalCount = await reviews.CountAsync(cancellationToken);
        var items = await reviews
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .SelectReviewDtosAsync(cancellationToken);

        return new PagedResult<DocumentReviewDto>(items, page, pageSize, totalCount);
    }

    public async Task<DocumentReviewContextDto> GetContextAsync(
        Guid documentId,
        int revisionNumber,
        CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureViewAsync(documentId, cancellationToken);
        var document = await documentProvider.GetAsync(documentId, cancellationToken);
        var revision = await revisionProvider.GetAsync(documentId, revisionNumber, cancellationToken);
        var previousRevision = revisionNumber > 1
            ? await revisionProvider.GetAsync(documentId, revisionNumber - 1, cancellationToken)
            : null;
        var comments = await commentProvider.ListAsync(documentId, revisionNumber, 1, 100, cancellationToken);
        var reviews = await BuildReviewQuery()
            .Where(x => x.KnowledgeItemRevisionId == revision.Id)
            .OrderBy(x => x.CreatedAt)
            .SelectReviewDtosAsync(cancellationToken);

        return new DocumentReviewContextDto(
            document,
            revision,
            previousRevision,
            comments.Items,
            reviews);
    }

    public async Task<DocumentReviewDto> DecideAsync(
        Guid reviewId,
        DecideDocumentReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var review = await BuildTrackedReviewQuery()
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Document review was not found.");

        if (review.ReviewerUserId != userId)
        {
            throw new ForbiddenException("Only the assigned reviewer can decide this review.");
        }

        RequirePending(review);
        if (!Enum.IsDefined(request.Decision))
        {
            throw new ValidationException("Review decision is invalid.");
        }

        var comment = CleanOptional(request.Comment, "Decision comment", 4000);
        if (request.Decision == DocumentReviewDecision.ChangesRequested && comment is null)
        {
            throw new ValidationException("A comment is required when requesting changes.");
        }

        var now = dateTimeProvider.UtcNow;
        review.Status = request.Decision == DocumentReviewDecision.Approved
            ? DocumentReviewStatus.Approved
            : DocumentReviewStatus.ChangesRequested;
        review.DecisionComment = comment;
        review.ReviewedAt = now;
        review.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return review.ToDto();
    }

    public async Task<DocumentReviewDto> CancelAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var review = await BuildTrackedReviewQuery()
            .FirstOrDefaultAsync(x => x.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException("Document review was not found.");

        var documentId = review.Revision?.KnowledgeItemId
            ?? throw new NotFoundException("Review revision was not found.");
        if (review.RequestedByUserId != userId &&
            !await documentAccessService.CanEditAsync(documentId, cancellationToken))
        {
            throw new ForbiddenException("Only the requester or a document editor can cancel this review.");
        }

        RequirePending(review);
        var now = dateTimeProvider.UtcNow;
        review.Status = DocumentReviewStatus.Cancelled;
        review.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return review.ToDto();
    }

    private IQueryable<DocumentRevisionReview> BuildReviewQuery()
    {
        return dbContext.DocumentRevisionReviews
            .AsNoTracking()
            .Include(x => x.Revision)
            .ThenInclude(x => x!.KnowledgeItem)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewerUser);
    }

    private IQueryable<DocumentRevisionReview> BuildTrackedReviewQuery()
    {
        return dbContext.DocumentRevisionReviews
            .Include(x => x.Revision)
            .ThenInclude(x => x!.KnowledgeItem)
            .Include(x => x.RequestedByUser)
            .Include(x => x.ReviewerUser);
    }

    private async Task ValidateReviewersAsync(
        KnowledgeItem? document,
        IReadOnlyCollection<Guid> reviewerIds,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (document is null)
        {
            throw new NotFoundException("Document was not found.");
        }

        if (document.Scope == DocumentScope.Personal)
        {
            if (reviewerIds.Any(x => x != currentUserId))
            {
                throw new ValidationException("Personal documents can only be reviewed by their owner.");
            }

            return;
        }

        var memberIds = await dbContext.ProjectMembers
            .Where(x => x.ProjectId == document.ProjectId && reviewerIds.Contains(x.UserId))
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        var missingIds = reviewerIds.Except(memberIds).ToArray();
        if (missingIds.Length > 0)
        {
            throw new ValidationException(
                $"Reviewer(s) are not project members: {string.Join(", ", missingIds)}.");
        }
    }

    private Guid RequireCurrentUser()
    {
        if (!currentUserContext.IsAuthenticated || currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return currentUserContext.UserId;
    }

    private static void RequirePending(DocumentRevisionReview review)
    {
        if (review.Status != DocumentReviewStatus.Pending)
        {
            throw new ConflictException("The document review has already been completed.");
        }
    }

    private static string? CleanOptional(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}

internal static class DocumentReviewQueryExtensions
{
    public static async Task<IReadOnlyList<DocumentReviewDto>> SelectReviewDtosAsync(
        this IQueryable<DocumentRevisionReview> query,
        CancellationToken cancellationToken)
    {
        var reviews = await query.ToListAsync(cancellationToken);
        return reviews.Select(x => x.ToDto()).ToArray();
    }
}
