using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class CommentProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentAccessService documentAccessService) : ICommentProvider
{
    public async Task<PagedResult<CommentDto>> ListAsync(Guid documentId, int revisionNumber, int page, int pageSize, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureViewAsync(documentId, cancellationToken);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var commentsQuery = dbContext.KnowledgeItemComments
            .AsNoTracking()
            .Include(x => x.AuthorUser)
            .Include(x => x.ResolvedByUser)
            .Include(x => x.Revision)
            .Where(x => x.Revision != null && x.Revision.KnowledgeItemId == documentId && x.Revision.RevisionNumber == revisionNumber);

        var totalCount = await commentsQuery.CountAsync(cancellationToken);
        var comments = await commentsQuery
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CommentDto>(
            comments.Select(x => x.ToDto()).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<CommentDto> AddAsync(Guid documentId, int revisionNumber, AddCommentRequest request, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureCommentAsync(documentId, cancellationToken);

        var userId = currentUserContext.RequireUserId();
        var revision = await dbContext.KnowledgeItemRevisions
            .FirstOrDefaultAsync(x => x.KnowledgeItemId == documentId && x.RevisionNumber == revisionNumber, cancellationToken)
            ?? throw new NotFoundException("Revision was not found.");

        if (request.ParentCommentId.HasValue)
        {
            var parent = await dbContext.KnowledgeItemComments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ParentCommentId.Value, cancellationToken)
                ?? throw new NotFoundException("Parent comment was not found.");

            if (parent.KnowledgeItemRevisionId != revision.Id)
            {
                throw new ValidationException("A reply must target a comment on the same revision.");
            }

            if (parent.DeletedAt is not null)
            {
                throw new ValidationException("Deleted comments cannot receive replies.");
            }
        }

        var now = dateTimeProvider.UtcNow;
        var comment = new KnowledgeItemComment
        {
            Id = Guid.NewGuid(),
            KnowledgeItemRevisionId = revision.Id,
            AuthorUserId = userId,
            ParentCommentId = request.ParentCommentId,
            Content = RequestText.Require(request.Content, "Comment", 4000),
            CreatedAt = now
        };

        dbContext.KnowledgeItemComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(comment.Id, cancellationToken);
    }

    public async Task<CommentDto> UpdateAsync(Guid commentId, UpdateCommentRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var comment = await dbContext.KnowledgeItemComments
            .Include(x => x.AuthorUser)
            .Include(x => x.ResolvedByUser)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment was not found.");

        if (comment.DeletedAt is not null)
        {
            throw new ValidationException("Deleted comments cannot be edited.");
        }

        if (comment.AuthorUserId != userId)
        {
            throw new ForbiddenException("You can only edit your own comments.");
        }

        comment.Content = RequestText.Require(request.Content, "Comment", 4000);
        comment.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return comment.ToDto();
    }

    public async Task<CommentDto> ResolveAsync(
        Guid commentId,
        ResolveCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var comment = await dbContext.KnowledgeItemComments
            .Include(x => x.AuthorUser)
            .Include(x => x.ResolvedByUser)
            .Include(x => x.Revision)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment was not found.");

        if (comment.DeletedAt is not null)
        {
            throw new ValidationException("Deleted comments cannot be resolved.");
        }

        var documentId = comment.Revision?.KnowledgeItemId
            ?? throw new NotFoundException("Comment revision was not found.");
        if (comment.AuthorUserId != userId &&
            !await documentAccessService.CanEditAsync(documentId, cancellationToken))
        {
            throw new ForbiddenException("Only the comment author or a document editor can resolve this comment.");
        }

        var now = dateTimeProvider.UtcNow;
        comment.ResolvedAt = request.IsResolved ? now : null;
        comment.ResolvedByUserId = request.IsResolved ? userId : null;
        comment.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(comment.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var comment = await dbContext.KnowledgeItemComments
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken)
            ?? throw new NotFoundException("Comment was not found.");

        if (comment.DeletedAt is not null)
        {
            return;
        }

        if (comment.AuthorUserId != userId)
        {
            throw new ForbiddenException("You can only delete your own comments.");
        }

        comment.DeletedAt = dateTimeProvider.UtcNow;
        comment.Content = string.Empty;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<CommentDto> ReloadAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var comment = await dbContext.KnowledgeItemComments
            .AsNoTracking()
            .Include(x => x.AuthorUser)
            .Include(x => x.ResolvedByUser)
            .Include(x => x.Revision)
            .FirstAsync(x => x.Id == commentId, cancellationToken);

        return comment.ToDto();
    }

}
