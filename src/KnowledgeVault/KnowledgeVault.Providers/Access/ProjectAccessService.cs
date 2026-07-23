using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

/// <summary>
/// Single owner of the "who can see / edit what" rules that used to be
/// re-implemented inside DocumentProvider, FolderProvider, DocumentAccessService,
/// DocumentReviewProvider and ProjectTopicProvider.
///
/// Role rules:
/// - Personal content is visible and editable only by its owner.
/// - Project content is visible to any project member (including Viewer).
/// - Project content is editable by Owner / Admin / Editor.
/// </summary>
public sealed class ProjectAccessService(KnowledgeVaultDbContext dbContext)
{
    /// <summary>True when the role may create/modify content inside a project.</summary>
    public static bool CanEditContent(ProjectRole? role) =>
        role is ProjectRole.Owner or ProjectRole.Admin or ProjectRole.Editor;

    /// <summary>Returns the user's role in the project, or null when not a member.</summary>
    public async Task<ProjectRole?> GetRoleAsync(Guid? projectId, Guid userId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return null;
        }

        var member = await dbContext.ProjectMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        return member?.Role;
    }

    /// <summary>Returns true when the user is a member of the project (any role).</summary>
    public async Task<bool> IsMemberAsync(Guid? projectId, Guid userId, CancellationToken cancellationToken)
    {
        return await GetRoleAsync(projectId, userId, cancellationToken) is not null;
    }

    /// <summary>Throws 403 unless the user may edit content (Owner/Admin/Editor) in the project.</summary>
    public async Task EnsureContentEditorAsync(
        Guid? projectId,
        Guid userId,
        string forbiddenMessage,
        CancellationToken cancellationToken)
    {
        var role = await GetRoleAsync(projectId, userId, cancellationToken);
        if (!CanEditContent(role))
        {
            throw new ForbiddenException(forbiddenMessage);
        }
    }

    /// <summary>
    /// Restricts a document query to non-deleted documents the user can see.
    /// scope == null means "personal + all member projects"; a non-null
    /// projectId additionally narrows project documents to that project.
    /// </summary>
    public IQueryable<KnowledgeItem> FilterAccessibleDocuments(
        IQueryable<KnowledgeItem> source,
        Guid userId,
        DocumentScope? scope,
        Guid? projectId = null)
    {
        source = source.Where(x => x.Status != KnowledgeItemStatus.Deleted);

        if (scope == DocumentScope.Personal)
        {
            return source.Where(x => x.Scope == DocumentScope.Personal && x.OwnerUserId == userId);
        }

        if (scope == DocumentScope.Project)
        {
            source = source.Where(x => x.Scope == DocumentScope.Project &&
                dbContext.ProjectMembers.Any(m => m.ProjectId == x.ProjectId && m.UserId == userId));

            return projectId is null ? source : source.Where(x => x.ProjectId == projectId);
        }

        return source.Where(x =>
            (x.Scope == DocumentScope.Personal && x.OwnerUserId == userId) ||
            (x.Scope == DocumentScope.Project && x.ProjectId != null &&
             dbContext.ProjectMembers.Any(m => m.ProjectId == x.ProjectId && m.UserId == userId)));
    }

    /// <summary>
    /// Restricts a folder query to folders the user can see, using the same
    /// membership rules as documents.
    /// </summary>
    public IQueryable<Folder> FilterAccessibleFolders(
        IQueryable<Folder> source,
        Guid userId,
        DocumentScope? scope,
        Guid? projectId = null)
    {
        if (scope == DocumentScope.Personal)
        {
            return source.Where(f => f.Scope == DocumentScope.Personal && f.OwnerUserId == userId);
        }

        if (scope == DocumentScope.Project)
        {
            source = source.Where(f => f.Scope == DocumentScope.Project &&
                dbContext.ProjectMembers.Any(m => m.ProjectId == f.ProjectId && m.UserId == userId));

            return projectId is null ? source : source.Where(f => f.ProjectId == projectId);
        }

        return source.Where(f =>
            (f.Scope == DocumentScope.Personal && f.OwnerUserId == userId) ||
            (f.Scope == DocumentScope.Project && f.ProjectId != null &&
             dbContext.ProjectMembers.Any(m => m.ProjectId == f.ProjectId && m.UserId == userId)));
    }
}
