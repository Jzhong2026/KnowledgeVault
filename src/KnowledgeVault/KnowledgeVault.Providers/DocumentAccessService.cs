using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class DocumentAccessService(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext) : IDocumentAccessService
{
    public async Task<bool> CanViewAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return false;
        }

        var doc = await LoadDocumentAsync(documentId, cancellationToken);
        if (doc is null)
        {
            return false;
        }

        return doc.Scope == DocumentScope.Personal
            ? doc.OwnerUserId == userId
            : await IsProjectMemberAsync(doc.TopicId, userId, cancellationToken);
    }

    public async Task<bool> CanEditAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return false;
        }

        var doc = await LoadDocumentAsync(documentId, cancellationToken);
        if (doc is null)
        {
            return false;
        }

        if (doc.Scope == DocumentScope.Personal)
        {
            return doc.OwnerUserId == userId;
        }

        var role = await GetProjectRoleAsync(doc.TopicId, userId, cancellationToken);
        return role is ProjectRole.Owner or ProjectRole.Editor;
    }

    public async Task<bool> CanCommentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return false;
        }

        var doc = await LoadDocumentAsync(documentId, cancellationToken);
        if (doc is null)
        {
            return false;
        }

        if (doc.Scope == DocumentScope.Personal)
        {
            return doc.OwnerUserId == userId;
        }

        return await IsProjectMemberAsync(doc.TopicId, userId, cancellationToken);
    }

    public async Task EnsureViewAsync(Guid documentId, CancellationToken cancellationToken)
    {
        if (!await CanViewAsync(documentId, cancellationToken))
        {
            throw new ForbiddenException("You do not have access to this document.");
        }
    }

    public async Task EnsureEditAsync(Guid documentId, CancellationToken cancellationToken)
    {
        if (!await CanEditAsync(documentId, cancellationToken))
        {
            throw new ForbiddenException("You do not have permission to modify this document.");
        }
    }

    public async Task EnsureCommentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        if (!await CanCommentAsync(documentId, cancellationToken))
        {
            throw new ForbiddenException("You do not have permission to comment on this document.");
        }
    }

    private Guid GetUserId()
    {
        return !currentUserContext.IsAuthenticated ? Guid.Empty : currentUserContext.UserId;
    }

    private async Task<KnowledgeItem?> LoadDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeItems
            .AsNoTracking()
            .Include(x => x.Topic)
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
    }

    private async Task<bool> IsProjectMemberAsync(Guid? topicId, Guid userId, CancellationToken cancellationToken)
    {
        return await GetProjectRoleAsync(topicId, userId, cancellationToken) is not null;
    }

    private async Task<ProjectRole?> GetProjectRoleAsync(Guid? topicId, Guid userId, CancellationToken cancellationToken)
    {
        if (topicId is null)
        {
            return null;
        }

        var projectId = await dbContext.ProjectTopics
            .Where(t => t.Id == topicId)
            .Select(t => t.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId == Guid.Empty)
        {
            return null;
        }

        var member = await dbContext.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        return member?.Role;
    }
}
