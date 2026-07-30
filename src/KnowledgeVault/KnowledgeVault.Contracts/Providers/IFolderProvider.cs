using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Providers;

public interface IFolderProvider
{
    Task<FolderContentDto> GetContentAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? parentFolderId,
        Guid? rootFolderId,
        bool includeArchived,
        CancellationToken cancellationToken);

    /// <summary>
    /// Paged variant of <see cref="GetContentAsync"/>. Folders and documents
    /// are paged independently (same <c>page</c>/<c>pageSize</c> applied to
    /// each stream) and ordered by <c>CreatedAt</c> DESC. Used by the
    /// workspace "Load more" UI; the unpaged <see cref="GetContentAsync"/>
    /// remains in use for full-content downloads.
    /// </summary>
    Task<FolderContentPagedDto> GetContentPagedAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? parentFolderId,
        Guid? rootFolderId,
        bool includeArchived,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<FolderTreeNodeDto> GetTreeAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? rootFolderId,
        CancellationToken cancellationToken);

    Task<FolderSummaryDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<FolderSummaryDto> CreateAsync(CreateFolderRequest request, CancellationToken cancellationToken);

    Task<FolderSummaryDto> UpdateAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid id, CancellationToken cancellationToken);

    Task RestoreAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FolderDescendantDto>> ListDescendantsForMcpAsync(Guid folderId, CancellationToken cancellationToken);
}
