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
        CancellationToken cancellationToken);

    Task<FolderTreeNodeDto> GetTreeAsync(
        DocumentScope? scope,
        Guid? projectId,
        Guid? rootFolderId,
        CancellationToken cancellationToken);

    Task<FolderSummaryDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<FolderSummaryDto> CreateAsync(CreateFolderRequest request, CancellationToken cancellationToken);

    Task<FolderSummaryDto> UpdateAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
