using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

/// <summary>
/// Per-document access checks (view / edit / comment). Membership and role
/// rules are delegated to <see cref="ProjectAccessService"/> so there is a
/// single implementation of the project access policy.
/// </summary>
public sealed class DocumentAccessService(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    ProjectAccessService projectAccess) : IDocumentAccessService
{
    public async Task<bool> CanViewAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty)
        {
            return false;
        }

        var doc = await LoadDocumentAsync(documentId, cancellationToken);
        if (doc is null || doc.Status == KnowledgeItemStatus.Deleted)
        {
            return false;
        }

        // Personal documents are visible only to their owner.
        if (doc.Scope == DocumentScope.Personal)
        {
            return doc.OwnerUserId == userId;
        }

        // Project documents are visible only to project members (any role,
        // including Viewer).
        return await projectAccess.IsMemberAsync(doc.ProjectId, userId, cancellationToken);
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

        var role = await projectAccess.GetRoleAsync(doc.ProjectId, userId, cancellationToken);
        return ProjectAccessService.CanEditContent(role);
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

        return await projectAccess.IsMemberAsync(doc.ProjectId, userId, cancellationToken);
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
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);
    }
}
