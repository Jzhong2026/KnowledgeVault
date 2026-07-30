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
    int DocumentCount,
    bool IsArchived);

public sealed record FolderTreeNodeDto(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    int SortOrder,
    IReadOnlyList<FolderTreeNodeDto> Children,
    bool IsArchived);

public sealed record FolderContentDto(
    IReadOnlyList<FolderSummaryDto> Folders,
    IReadOnlyList<KnowledgeItemSummaryDto> Documents);

public sealed record FolderDescendantDto(
    Guid Id,
    string Type,
    string Name,
    Guid? ParentFolderId,
    string Path);

/// <summary>
/// Paged view of a folder's direct children. Folders and documents are
/// paged independently because they live in different tables; both streams
/// share the same <see cref="Page"/> / <see cref="PageSize"/> so a single
/// "Load more" action reveals <see cref="PageSize"/> more of each. Counts
/// stay separate (<see cref="TotalFolderCount"/> + <see cref="TotalDocumentCount"/>)
/// so the UI can render independent totals or hide one of the streams
/// when it is empty.
/// </summary>
public sealed record FolderContentPagedDto(
    IReadOnlyList<FolderSummaryDto> Folders,
    IReadOnlyList<KnowledgeItemSummaryDto> Documents,
    int Page,
    int PageSize,
    int TotalFolderCount,
    int TotalDocumentCount)
{
    public bool HasMoreFolders => PageSize > 0 && Page * PageSize < TotalFolderCount;
    public bool HasMoreDocuments => PageSize > 0 && Page * PageSize < TotalDocumentCount;
    public bool HasMore => HasMoreFolders || HasMoreDocuments;
}

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
