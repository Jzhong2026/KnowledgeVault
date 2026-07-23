using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

/// <summary>
/// Folder use cases. Visibility rules are delegated to
/// <see cref="ProjectAccessService"/> so folders and documents share one
/// access policy implementation.
/// </summary>
public sealed class FolderProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    ProjectAccessService projectAccess) : IFolderProvider
{
    public async Task<FolderContentDto> GetContentAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? parentFolderId,
        Guid? rootFolderId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();

        if (rootFolderId.HasValue)
        {
            await EnsureFolderAccessibleAsync(rootFolderId.Value, userId, cancellationToken);
            if (!await IsWithinRootAsync(parentFolderId, rootFolderId.Value, cancellationToken))
            {
                throw new ValidationException("The requested folder is outside of the workspace root.");
            }
        }

        var folders = await QueryAccessibleFolders(userId, scope, projectId)
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var documents = await QueryAccessibleDocuments(userId, scope, projectId)
            .Where(x => x.FolderId == parentFolderId)
            .Include(x => x.OwnerUser)
            .Include(x => x.Project)
            .Include(x => x.Topic)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags).ThenInclude(t => t.Tag)
            .Include(x => x.CurrentRevision)
            .ToListAsync(cancellationToken);

        documents = documents
            .OrderBy(d => d.CurrentRevision?.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var folderIds = folders.Select(f => f.Id).ToList();
        var childCounts = await dbContext.Folders
            .Where(f => f.ParentFolderId != null && folderIds.Contains(f.ParentFolderId.Value))
            .GroupBy(f => f.ParentFolderId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        var docCounts = await dbContext.KnowledgeItems
            .Where(x => x.Status != KnowledgeItemStatus.Deleted && x.FolderId != null && folderIds.Contains(x.FolderId.Value))
            .GroupBy(x => x.FolderId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var folderDtos = folders.Select(f =>
        {
            childCounts.TryGetValue(f.Id, out var cc);
            docCounts.TryGetValue(f.Id, out var dc);
            return new FolderSummaryDto(
                f.Id, f.Name, f.Description, f.SortOrder, f.ParentFolderId, f.ProjectId, f.Scope, cc, dc);
        }).ToArray();

        return new FolderContentDto(folderDtos, documents.Select(x => x.ToSummaryDto()).ToArray());
    }

    public async Task<FolderTreeNodeDto> GetTreeAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? rootFolderId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        if (!rootFolderId.HasValue)
        {
            throw new ValidationException("rootFolderId is required to build the folder tree.");
        }

        await EnsureFolderAccessibleAsync(rootFolderId.Value, userId, cancellationToken);

        var all = await QueryAccessibleFolders(userId, scope, projectId).ToListAsync(cancellationToken);
        var within = new HashSet<Guid>();
        foreach (var f in all)
        {
            if (await IsWithinRootAsync(f.Id, rootFolderId.Value, cancellationToken))
            {
                within.Add(f.Id);
            }
        }

        var nodes = all
            .Where(f => within.Contains(f.Id))
            .ToDictionary(f => f.Id, f => new FolderTreeNodeDto(f.Id, f.Name, f.ParentFolderId, f.SortOrder, new List<FolderTreeNodeDto>()));

        foreach (var f in all.Where(f => within.Contains(f.Id)))
        {
            if (f.ParentFolderId is not null &&
                f.ParentFolderId != rootFolderId &&
                nodes.ContainsKey(f.ParentFolderId.Value))
            {
                var children = (List<FolderTreeNodeDto>)nodes[f.ParentFolderId.Value].Children;
                children.Add(nodes[f.Id]);
            }
        }

        foreach (var node in nodes.Values)
        {
            var children = (List<FolderTreeNodeDto>)node.Children;
            children.Sort((a, b) => a.SortOrder != b.SortOrder
                ? a.SortOrder.CompareTo(b.SortOrder)
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }

        return nodes[rootFolderId.Value];
    }

    public async Task<FolderSummaryDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        var childCount = await dbContext.Folders.CountAsync(f => f.ParentFolderId == id, cancellationToken);
        var docCount = await dbContext.KnowledgeItems.CountAsync(
            x => x.FolderId == id && x.Status != KnowledgeItemStatus.Deleted, cancellationToken);

        return new FolderSummaryDto(
            folder.Id, folder.Name, folder.Description, folder.SortOrder,
            folder.ParentFolderId, folder.ProjectId, folder.Scope, childCount, docCount);
    }

    public async Task<FolderSummaryDto> CreateAsync(CreateFolderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var now = dateTimeProvider.UtcNow;
        var name = RequestText.Require(request.Name, "Name", 128);
        var normalized = TextNormalizer.NormalizeName(name);

        Guid? ownerUserId = null;
        if (request.Scope == DocumentScope.Personal)
        {
            if (request.ProjectId is not null)
            {
                throw new ValidationException("Personal folders cannot belong to a project.");
            }

            ownerUserId = userId;
        }
        else
        {
            if (request.ProjectId is null || request.ProjectId == Guid.Empty)
            {
                throw new ValidationException("Project is required for project folders.");
            }

            await projectAccess.EnsureContentEditorAsync(
                request.ProjectId.Value,
                userId,
                "You do not have permission to create folders in this project.",
                cancellationToken);
        }

        if (request.ParentFolderId.HasValue)
        {
            var parent = await EnsureFolderAccessibleAsync(request.ParentFolderId.Value, userId, cancellationToken);
            if (parent.Scope != request.Scope || parent.ProjectId != request.ProjectId)
            {
                throw new ValidationException("The parent folder does not belong to the same scope/project.");
            }

            if (request.Scope == DocumentScope.Personal && parent.OwnerUserId != userId)
            {
                throw new ForbiddenException("You do not have permission to create folders here.");
            }
        }

        await EnsureUniqueSiblingAsync(
            request.Scope, request.ProjectId, ownerUserId, request.ParentFolderId, normalized, null, cancellationToken);

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Scope = request.Scope,
            ProjectId = request.Scope == DocumentScope.Project ? request.ProjectId : null,
            OwnerUserId = ownerUserId,
            ParentFolderId = request.ParentFolderId,
            Name = name,
            NormalizedName = normalized,
            Description = RequestText.Optional(request.Description, 512),
            SortOrder = request.SortOrder,
            CreatedAt = now
        };

        dbContext.Folders.Add(folder);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FolderSummaryDto(
            folder.Id, folder.Name, folder.Description, folder.SortOrder,
            folder.ParentFolderId, folder.ProjectId, folder.Scope, 0, 0);
    }

    public async Task<FolderSummaryDto> UpdateAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        string? newName = null;
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            newName = RequestText.Require(request.Name, "Name", 128);
            normalized = TextNormalizer.NormalizeName(newName);
        }

        if (request.ParentFolderId.HasValue)
        {
            if (request.ParentFolderId.Value == id)
            {
                throw new ValidationException("A folder cannot be its own parent.");
            }

            var parent = await EnsureFolderAccessibleAsync(request.ParentFolderId.Value, userId, cancellationToken);
            if (parent.Scope != folder.Scope || parent.ProjectId != folder.ProjectId)
            {
                throw new ValidationException("The target parent folder does not belong to the same scope/project.");
            }

            // Detect a cycle: reject if the requested new parent is itself a
            // descendant of the folder being moved (i.e. the folder is an ancestor
            // of the new parent). IsWithinRootAsync(newParent, id) returns true when
            // newParent lies within id's subtree.
            if (await IsWithinRootAsync(request.ParentFolderId.Value, id, cancellationToken))
            {
                throw new ValidationException("A folder cannot be moved into one of its own subfolders.");
            }
        }

        if (normalized is not null && normalized != folder.NormalizedName)
        {
            var siblingParent = request.ParentFolderId ?? folder.ParentFolderId;
            await EnsureUniqueSiblingAsync(
                folder.Scope, folder.ProjectId, folder.OwnerUserId, siblingParent, normalized, id, cancellationToken);
        }

        if (newName is not null)
        {
            folder.Name = newName;
            folder.NormalizedName = normalized!;
        }

        if (request.Description is not null)
        {
            folder.Description = RequestText.Optional(request.Description, 512);
        }

        if (request.ParentFolderId.HasValue)
        {
            folder.ParentFolderId = request.ParentFolderId;
        }

        if (request.SortOrder.HasValue)
        {
            folder.SortOrder = request.SortOrder.Value;
        }

        folder.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var childCount = await dbContext.Folders.CountAsync(f => f.ParentFolderId == id, cancellationToken);
        var docCount = await dbContext.KnowledgeItems.CountAsync(
            x => x.FolderId == id && x.Status != KnowledgeItemStatus.Deleted, cancellationToken);

        return new FolderSummaryDto(
            folder.Id, folder.Name, folder.Description, folder.SortOrder,
            folder.ParentFolderId, folder.ProjectId, folder.Scope, childCount, docCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        var hasChildren = await dbContext.Folders.AnyAsync(f => f.ParentFolderId == id, cancellationToken);
        var hasDocs = await dbContext.KnowledgeItems.AnyAsync(
            x => x.FolderId == id && x.Status != KnowledgeItemStatus.Deleted, cancellationToken);

        if (hasChildren || hasDocs)
        {
            throw new ConflictException(
                "Cannot delete a folder that is not empty. Move or delete its contents first.");
        }

        dbContext.Folders.Remove(folder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Folder> QueryAccessibleFolders(Guid userId, DocumentScope? scope, Guid? projectId)
    {
        EnsureProjectIdPresentForProjectScope(scope, projectId);
        return projectAccess.FilterAccessibleFolders(
            dbContext.Folders.AsNoTracking(), userId, scope, projectId);
    }

    private IQueryable<KnowledgeItem> QueryAccessibleDocuments(Guid userId, DocumentScope? scope, Guid? projectId)
    {
        EnsureProjectIdPresentForProjectScope(scope, projectId);
        return projectAccess.FilterAccessibleDocuments(
            dbContext.KnowledgeItems.AsNoTracking(), userId, scope, projectId);
    }

    private static void EnsureProjectIdPresentForProjectScope(DocumentScope? scope, Guid? projectId)
    {
        if (scope == DocumentScope.Project && projectId is null)
        {
            throw new ValidationException("Project is required for project folders.");
        }
    }

    private async Task<Folder> EnsureFolderAccessibleAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        var folder = await dbContext.Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new NotFoundException("Folder was not found.");

        var accessible = await QueryAccessibleFolders(userId, folder.Scope, folder.ProjectId)
            .AnyAsync(f => f.Id == folderId, cancellationToken);

        if (!accessible)
        {
            throw new ForbiddenException("You do not have access to this folder.");
        }

        return folder;
    }

    private async Task EnsureCanEditFolderAsync(Folder folder, Guid userId, CancellationToken cancellationToken)
    {
        if (folder.Scope == DocumentScope.Personal)
        {
            if (folder.OwnerUserId != userId)
            {
                throw new ForbiddenException("You do not have permission to modify this folder.");
            }

            return;
        }

        await projectAccess.EnsureContentEditorAsync(
            folder.ProjectId,
            userId,
            "You do not have permission to modify this folder.",
            cancellationToken);
    }

    private async Task EnsureUniqueSiblingAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? ownerUserId,
        Guid? parentFolderId,
        string normalized,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var existing = dbContext.Folders.AsNoTracking()
            .Where(f => f.Scope == scope && f.NormalizedName == normalized && f.ParentFolderId == parentFolderId);

        if (scope == DocumentScope.Personal)
        {
            existing = existing.Where(f => f.OwnerUserId == ownerUserId);
        }
        else
        {
            existing = existing.Where(f => f.ProjectId == projectId);
        }

        if (excludeId.HasValue)
        {
            existing = existing.Where(f => f.Id != excludeId.Value);
        }

        if (await existing.AnyAsync(cancellationToken))
        {
            throw new ValidationException("A folder with this name already exists at this level.");
        }
    }

    private async Task<bool> IsWithinRootAsync(Guid? folderId, Guid rootId, CancellationToken cancellationToken)
    {
        if (folderId is null)
        {
            return true;
        }

        var current = await dbContext.Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);
        while (current is not null)
        {
            if (current.Id == rootId)
            {
                return true;
            }

            if (current.ParentFolderId is null)
            {
                return false;
            }

            current = await dbContext.Folders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == current.ParentFolderId, cancellationToken);
        }

        return false;
    }
}
