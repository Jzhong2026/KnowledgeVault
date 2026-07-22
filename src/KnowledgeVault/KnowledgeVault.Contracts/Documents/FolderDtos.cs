using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Documents;

public sealed record FolderSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    Guid? ParentFolderId,
    Guid? ProjectId,
    DocumentScope Scope,
    int ChildFolderCount,
    int DocumentCount);

public sealed record FolderTreeNodeDto(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    int SortOrder,
    IReadOnlyList<FolderTreeNodeDto> Children);

public sealed record FolderContentDto(
    IReadOnlyList<FolderSummaryDto> Folders,
    IReadOnlyList<KnowledgeItemSummaryDto> Documents);

public sealed record CreateFolderRequest(
    DocumentScope Scope,
    Guid? ProjectId,
    Guid? ParentFolderId,
    string Name,
    string? Description,
    int SortOrder = 0);

public sealed record UpdateFolderRequest(
    string? Name,
    string? Description,
    Guid? ParentFolderId,
    int? SortOrder);
