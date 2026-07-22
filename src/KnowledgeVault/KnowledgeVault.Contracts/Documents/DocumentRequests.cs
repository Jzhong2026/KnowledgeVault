using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Documents;

public sealed record DocumentQuery(
    DocumentScope? Scope,
    Guid? ProjectId,
    Guid? TopicId,
    DocumentType? DocumentType,
    string? LinkDisplayText,
    string? Search,
    Guid? CategoryId,
    Guid? OwnerUserId,
    KnowledgeItemStatus? Status,
    IReadOnlyList<Guid>? TagIds,
    DocumentSort? Sort,
    int Page = 1,
    int PageSize = 20,
    Guid? FolderId = null);

public sealed record CreateDocumentRequest(
    DocumentScope Scope,
    Guid? ProjectId,
    Guid? TopicId,
    DocumentType DocumentType,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    string? LinkDisplayText,
    string? LinkUrl,
    string? ChangeNote,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames,
    Guid? FolderId = null);

public sealed record UpdateDocumentRequest(
    int ExpectedRevisionNumber,
    Guid? ProjectId,
    Guid? TopicId,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    string? LinkDisplayText,
    string? LinkUrl,
    string? ChangeNote,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames,
    Guid? FolderId = null);

public sealed record UpdateDocumentMetadataRequest(
    Guid? ProjectId,
    Guid? TopicId,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames,
    Guid? FolderId = null);

// Explicit document move. folderId = null moves the document to the root (no folder);
// a non-null folderId moves it into that folder. Scope/project consistency and write
// permission are validated by the provider.
public sealed record MoveDocumentRequest(Guid? FolderId);
