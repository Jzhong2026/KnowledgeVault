using KnowledgeVault.Contracts.Common;
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
    // Compatibility overload for existing direct provider callers. API callers
    // use the explicit includeArchived switch.
    public Task<FolderContentDto> GetContentAsync(
        DocumentScope? scope, Guid? projectId, Guid? parentFolderId, Guid? rootFolderId,
        CancellationToken cancellationToken) =>
        GetContentAsync(scope, projectId, parentFolderId, rootFolderId, false, null, null, cancellationToken);

    public async Task<FolderContentDto> GetContentAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? parentFolderId,
        Guid? rootFolderId,
        bool includeArchived,
        string? search,
        Guid? ownerUserId,
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

        // Order by CreatedAt descending so newest items surface first. The
        // user-facing list sorts deterministically off the backend so the
        // tile grid does not need a client-side fallback. Folders and
        // documents are ordered separately because they live in different
        // tables and EF cannot mix them into a single ORDER BY.
        search = RequestText.Optional(search, "search", 200);

        var foldersQuery = QueryAccessibleFolders(userId, scope, projectId)
            .Where(f => includeArchived || !f.IsArchived)
            .Where(f => f.ParentFolderId == parentFolderId);
        if (search is not null)
        {
            foldersQuery = foldersQuery.Where(f => f.Name.Contains(search));
        }

        var folders = await foldersQuery
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Name)
            .ToListAsync(cancellationToken);

        var documentsQuery = QueryAccessibleDocuments(userId, scope, projectId)
            .Where(x => x.Status != KnowledgeItemStatus.Deleted && (includeArchived || x.Status != KnowledgeItemStatus.Archived))
            .Where(x => x.FolderId == parentFolderId);

        if (ownerUserId.HasValue)
        {
            documentsQuery = documentsQuery.Where(x => x.OwnerUserId == ownerUserId.Value);
        }
        if (search is not null)
        {
            documentsQuery = documentsQuery.Where(x =>
                x.CurrentRevision != null && x.CurrentRevision.Title.Contains(search));
        }

        var documents = await documentsQuery
            .Include(x => x.OwnerUser)
            .Include(x => x.Project)
            .Include(x => x.Topic)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags).ThenInclude(t => t.Tag)
            .Include(x => x.CurrentRevision)
            .ToListAsync(cancellationToken);

        documents = documents
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.CurrentRevision?.Title ?? string.Empty, StringComparer.OrdinalIgnoreCase)
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
                f.Id, f.Name, f.Description, f.SortOrder, f.ParentFolderId, f.ProjectId, f.Scope, cc, dc, f.IsArchived);
        }).ToArray();

        return new FolderContentDto(folderDtos, documents.Select(x => x.ToSummaryDto()).ToArray());
    }

    public async Task<FolderContentPagedDto> GetContentPagedAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? parentFolderId,
        Guid? rootFolderId,
        bool includeArchived,
        string? search,
        Guid? ownerUserId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        search = RequestText.Optional(search, "search", 200);

        if (rootFolderId.HasValue)
        {
            await EnsureFolderAccessibleAsync(rootFolderId.Value, userId, cancellationToken);
            if (!await IsWithinRootAsync(parentFolderId, rootFolderId.Value, cancellationToken))
            {
                throw new ValidationException("The requested folder is outside of the workspace root.");
            }
        }

        // Folders page: ordered by CreatedAt DESC, name as tie-breaker.
        var foldersQuery = QueryAccessibleFolders(userId, scope, projectId)
            .Where(f => includeArchived || !f.IsArchived)
            .Where(f => f.ParentFolderId == parentFolderId);
        if (search is not null)
        {
            foldersQuery = foldersQuery.Where(f => f.Name.Contains(search));
        }

        var totalFolderCount = await foldersQuery.CountAsync(cancellationToken);
        var folders = await foldersQuery
            .OrderByDescending(f => f.CreatedAt)
            .ThenBy(f => f.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Documents page: ordered by CreatedAt DESC, title as tie-breaker.
        var documentsQuery = QueryAccessibleDocuments(userId, scope, projectId)
            .Where(x => x.Status != KnowledgeItemStatus.Deleted && (includeArchived || x.Status != KnowledgeItemStatus.Archived))
            .Where(x => x.FolderId == parentFolderId);

        if (ownerUserId.HasValue)
        {
            documentsQuery = documentsQuery.Where(x => x.OwnerUserId == ownerUserId.Value);
        }
        if (search is not null)
        {
            documentsQuery = documentsQuery.Where(x =>
                x.CurrentRevision != null && x.CurrentRevision.Title.Contains(search));
        }

        var totalDocumentCount = await documentsQuery.CountAsync(cancellationToken);
        var documents = await documentsQuery
            .Include(x => x.OwnerUser)
            .Include(x => x.Project)
            .Include(x => x.Topic)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags).ThenInclude(t => t.Tag)
            .Include(x => x.CurrentRevision)
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.CurrentRevision != null ? x.CurrentRevision.Title : string.Empty)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var folderIds = folders.Select(f => f.Id).ToList();
        var childCounts = folderIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.Folders
                .Where(f => f.ParentFolderId != null && folderIds.Contains(f.ParentFolderId.Value))
                .GroupBy(f => f.ParentFolderId!.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);
        var docCounts = folderIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.KnowledgeItems
                .Where(x => x.Status != KnowledgeItemStatus.Deleted && x.FolderId != null && folderIds.Contains(x.FolderId.Value))
                .GroupBy(x => x.FolderId!.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count(), cancellationToken);

        var folderDtos = folders.Select(f =>
        {
            childCounts.TryGetValue(f.Id, out var cc);
            docCounts.TryGetValue(f.Id, out var dc);
            return new FolderSummaryDto(
                f.Id, f.Name, f.Description, f.SortOrder, f.ParentFolderId, f.ProjectId, f.Scope, cc, dc, f.IsArchived);
        }).ToArray();

        return new FolderContentPagedDto(
            folderDtos,
            documents.Select(x => x.ToSummaryDto()).ToArray(),
            page,
            pageSize,
            totalFolderCount,
            totalDocumentCount);
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
            .ToDictionary(f => f.Id, f => new FolderTreeNodeDto(f.Id, f.Name, f.ParentFolderId, f.SortOrder, new List<FolderTreeNodeDto>(), f.IsArchived));

        foreach (var f in all.Where(f => within.Contains(f.Id)))
        {
            // A folder within the root is linked to its parent's Children list.
            // The parent is always present in `nodes` (the root itself, or another
            // within-root folder), so direct children of the root are attached to the
            // root node. The previous `f.ParentFolderId != rootFolderId` guard wrongly
            // dropped every direct child of the workspace root.
            if (f.ParentFolderId is not null &&
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
            folder.ParentFolderId, folder.ProjectId, folder.Scope, childCount, docCount, folder.IsArchived);
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

        // Full-path uniqueness: the proposed folder's normalized path string
        // (e.g. "/DOCS/PLANNING") must not collide with any existing folder at
        // the same scope. Sibling-only checks miss cases where a parent chain
        // is renamed above, so re-resolve the path against the live tree.
        var newPath = await BuildFolderPathAsync(
            request.Scope, request.ProjectId, ownerUserId, request.ParentFolderId, normalized, excludeId: null, cancellationToken);
        await EnsurePathAvailableAsync(request.Scope, request.ProjectId, ownerUserId, newPath, excludeId: null, cancellationToken);

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
            folder.ParentFolderId, folder.ProjectId, folder.Scope, 0, 0, folder.IsArchived);
    }

    public async Task<FolderSummaryDto> UpdateAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        // EnsureFolderAccessibleAsync returns a no-tracking snapshot, so
        // mutating it and calling SaveChanges would silently discard the
        // changes. Re-fetch the folder with tracking enabled so the
        // property assignments below are persisted on save.
        var tracked = await dbContext.Folders.FirstAsync(f => f.Id == id, cancellationToken);

        string? newName = null;
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            newName = RequestText.Require(request.Name, "Name", 128);
            normalized = TextNormalizer.NormalizeName(newName);
        }

        Guid? newParentId = request.ParentFolderId;
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
        else
        {
            newParentId = folder.ParentFolderId;
        }

        // If either the leaf name or parent changed, recompute the full path and
        // ensure it does not collide with any other folder in the same scope.
        if (normalized is not null || request.ParentFolderId.HasValue)
        {
            var effectiveName = normalized ?? folder.NormalizedName;
            var newPath = await BuildFolderPathAsync(
                folder.Scope, folder.ProjectId, folder.OwnerUserId, newParentId, effectiveName, excludeId: id, cancellationToken);
            await EnsurePathAvailableAsync(folder.Scope, folder.ProjectId, folder.OwnerUserId, newPath, excludeId: id, cancellationToken);
        }

        if (newName is not null)
        {
            tracked.Name = newName;
            tracked.NormalizedName = normalized!;
        }

        if (request.Description is not null)
        {
            tracked.Description = RequestText.Optional(request.Description, 512);
        }

        if (request.ParentFolderId.HasValue)
        {
            tracked.ParentFolderId = request.ParentFolderId;
        }

        if (request.SortOrder.HasValue)
        {
            tracked.SortOrder = request.SortOrder.Value;
        }

        tracked.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var childCount = await dbContext.Folders.CountAsync(f => f.ParentFolderId == id, cancellationToken);
        var docCount = await dbContext.KnowledgeItems.CountAsync(
            x => x.FolderId == id && x.Status != KnowledgeItemStatus.Deleted, cancellationToken);

        return new FolderSummaryDto(
            tracked.Id, tracked.Name, tracked.Description, tracked.SortOrder,
            tracked.ParentFolderId, tracked.ProjectId, tracked.Scope, childCount, docCount, tracked.IsArchived);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var folderIds = await GetDescendantFolderIdsAsync(id, cancellationToken);
        var folders = await dbContext.Folders.Where(f => folderIds.Contains(f.Id)).ToListAsync(cancellationToken);
        foreach (var tracked in folders)
        {
            tracked.IsArchived = true;
            tracked.ArchivedAt ??= now;
            tracked.UpdatedAt = now;
        }

        var documents = await dbContext.KnowledgeItems
            .Where(x => x.FolderId.HasValue && folderIds.Contains(x.FolderId.Value) && x.Status != KnowledgeItemStatus.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            document.ChangeStatus(KnowledgeItemStatus.Archived, now);
            document.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var folderIds = await GetDescendantFolderIdsAsync(id, cancellationToken);
        var folders = await dbContext.Folders.Where(f => folderIds.Contains(f.Id)).ToListAsync(cancellationToken);
        foreach (var tracked in folders)
        {
            tracked.IsArchived = false;
            tracked.ArchivedAt = null;
            tracked.UpdatedAt = now;
        }

        var documents = await dbContext.KnowledgeItems
            .Where(x => x.FolderId.HasValue && folderIds.Contains(x.FolderId.Value) && x.Status != KnowledgeItemStatus.Deleted)
            .ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            document.ChangeStatus(KnowledgeItemStatus.Active, now);
            document.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FolderDescendantDto>> ListDescendantsForMcpAsync(Guid folderId, CancellationToken cancellationToken)
    {
        var allFolders = await dbContext.Folders.AsNoTracking().ToListAsync(cancellationToken);
        var root = allFolders.FirstOrDefault(f => f.Id == folderId && !f.IsArchived);
        if (root is null || HasArchivedAncestor(root, allFolders))
        {
            return [];
        }

        var childrenByParent = allFolders
            .Where(f => !f.IsArchived && f.ParentFolderId.HasValue)
            .GroupBy(f => f.ParentFolderId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToArray());

        var paths = new Dictionary<Guid, string>();
        var queue = new Queue<(Guid FolderId, string Path)>();
        queue.Enqueue((root.Id, string.Empty));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current.FolderId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                var path = string.IsNullOrEmpty(current.Path) ? child.Name : $"{current.Path}/{child.Name}";
                paths[child.Id] = path;
                queue.Enqueue((child.Id, path));
            }
        }

        var folderItems = allFolders
            .Where(f => paths.ContainsKey(f.Id))
            .Select(f => new FolderDescendantDto(f.Id, "folder", f.Name, f.ParentFolderId, paths[f.Id]));
        var documentParentPaths = new Dictionary<Guid, string>(paths) { [root.Id] = string.Empty };
        var documentParentIds = documentParentPaths.Keys.ToArray();
        var documents = await dbContext.KnowledgeItems.AsNoTracking()
            .Include(x => x.CurrentRevision)
            .Where(x => x.FolderId.HasValue && documentParentIds.Contains(x.FolderId.Value) &&
                x.Status != KnowledgeItemStatus.Archived && x.Status != KnowledgeItemStatus.Deleted)
            .ToListAsync(cancellationToken);
        var documentItems = documents.Select(x =>
        {
            var name = x.CurrentRevision?.Title ?? $"document-{x.Id:D}";
            var parentPath = documentParentPaths[x.FolderId!.Value];
            var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";
            return new FolderDescendantDto(x.Id, "document", name, x.FolderId, path);
        });

        return folderItems.Concat(documentItems)
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasArchivedAncestor(Folder folder, IReadOnlyCollection<Folder> allFolders)
    {
        var byId = allFolders.ToDictionary(f => f.Id);
        var parentId = folder.ParentFolderId;
        while (parentId.HasValue && byId.TryGetValue(parentId.Value, out var parent))
        {
            if (parent.IsArchived)
            {
                return true;
            }
            parentId = parent.ParentFolderId;
        }
        return false;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var folder = await EnsureFolderAccessibleAsync(id, userId, cancellationToken);
        await EnsureCanEditFolderAsync(folder, userId, cancellationToken);

        // Block deletion when the folder still has child folders or non-deleted
        // documents. Callers must move or remove the contents first, or use
        // ArchiveAsync for a recursive soft-delete.
        var hasChildren = await dbContext.Folders
            .AnyAsync(f => f.ParentFolderId == id, cancellationToken);
        if (hasChildren)
        {
            throw new ConflictException("Cannot delete a folder that still contains subfolders. Remove or move them first.");
        }

        var hasDocuments = await dbContext.KnowledgeItems
            .AnyAsync(x => x.FolderId == id && x.Status != KnowledgeItemStatus.Deleted, cancellationToken);
        if (hasDocuments)
        {
            throw new ConflictException("Cannot delete a folder that still contains documents. Remove or move them first.");
        }

        await ArchiveAsync(id, cancellationToken);
    }

    private async Task<HashSet<Guid>> GetDescendantFolderIdsAsync(Guid rootFolderId, CancellationToken cancellationToken)
    {
        var allFolders = await dbContext.Folders.AsNoTracking()
            .Select(f => new { f.Id, f.ParentFolderId })
            .ToListAsync(cancellationToken);
        var childrenByParent = allFolders
            .Where(f => f.ParentFolderId.HasValue)
            .GroupBy(f => f.ParentFolderId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(f => f.Id).ToArray());
        var result = new HashSet<Guid> { rootFolderId };
        var queue = new Queue<Guid>();
        queue.Enqueue(rootFolderId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children))
            {
                continue;
            }
            foreach (var child in children)
            {
                if (result.Add(child))
                {
                    queue.Enqueue(child);
                }
            }
        }
        return result;
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

    /// <summary>
    /// Walks the proposed parent chain and returns the normalized path string for
    /// the new/renamed folder (e.g. "/DOCS/PLANNING"). Path segments are joined
    /// with "/", each already normalized via TextNormalizer (trim + UPPER_INVARIANT).
    /// </summary>
    private async Task<string> BuildFolderPathAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? ownerUserId,
        Guid? parentFolderId,
        string normalizedLeaf,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var segments = new List<string> { normalizedLeaf };
        var currentId = parentFolderId;
        var safety = 0;
        const int maxDepth = 64;

        while (currentId.HasValue)
        {
            if (++safety > maxDepth)
            {
                // Defensive: a corrupted cycle should never reach a depth past the
                // tree's own bounds. Bail out rather than spin forever.
                throw new ValidationException("Folder hierarchy is too deep to resolve.");
            }

            var parent = await dbContext.Folders.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == currentId.Value, cancellationToken);

            if (parent is null || parent.Scope != scope || parent.ProjectId != projectId)
            {
                throw new ValidationException("The parent folder does not belong to the same scope/project.");
            }

            if (scope == DocumentScope.Personal && parent.OwnerUserId != ownerUserId)
            {
                throw new ForbiddenException("You do not have permission to create folders here.");
            }

            segments.Add(parent.NormalizedName);
            currentId = parent.ParentFolderId;
        }

        segments.Reverse();
        return "/" + string.Join("/", segments);
    }

    /// <summary>
    /// Full-path uniqueness check scoped to one project (or one personal owner).
    /// Rejects collisions regardless of where the colliding folder lives in the
    /// tree, since the comparison key is the slash-joined path string.
    /// </summary>
    private async Task EnsurePathAvailableAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? ownerUserId,
        string proposedPath,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var folders = dbContext.Folders.AsNoTracking().Where(f => f.Scope == scope);
        if (scope == DocumentScope.Personal)
        {
            folders = folders.Where(f => f.OwnerUserId == ownerUserId);
        }
        else
        {
            folders = folders.Where(f => f.ProjectId == projectId);
        }

        if (excludeId.HasValue)
        {
            folders = folders.Where(f => f.Id != excludeId.Value);
        }

        // Fetch the small set of candidate folders and resolve their paths in
        // memory. The total folder count per project is small enough that this
        // is cheaper than recursive SQL.
        var candidates = await folders.Select(f => new PathNode(f.Id, f.ParentFolderId, f.NormalizedName)).ToListAsync(cancellationToken);
        var byId = candidates.ToDictionary(c => c.Id);
        foreach (var f in candidates)
        {
            if (TryResolvePath(f.Id, f.ParentFolderId, f.NormalizedName, byId, out var path) && string.Equals(path, proposedPath, StringComparison.Ordinal))
            {
                throw new ConflictException("A folder with this path already exists in this project.");
            }
        }
    }

    private readonly record struct PathNode(Guid Id, Guid? ParentFolderId, string NormalizedName);

    private bool TryResolvePath(
        Guid id,
        Guid? parentId,
        string normalizedLeaf,
        IReadOnlyDictionary<Guid, PathNode> folders,
        out string path)
    {
        var segments = new List<string> { normalizedLeaf };
        var currentId = parentId;
        var safety = 0;
        const int maxDepth = 64;

        while (currentId.HasValue)
        {
            if (++safety > maxDepth)
            {
                path = string.Empty;
                return false;
            }

            if (!folders.TryGetValue(currentId.Value, out var parent))
            {
                path = string.Empty;
                return false;
            }

            segments.Add(parent.NormalizedName);
            currentId = parent.ParentFolderId;
        }

        segments.Reverse();
        path = "/" + string.Join("/", segments);
        return true;
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
