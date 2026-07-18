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
    int PageSize = 20);

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
    IReadOnlyList<string>? TagNames);

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
    IReadOnlyList<string>? TagNames);

public sealed record UpdateDocumentMetadataRequest(
    Guid? ProjectId,
    Guid? TopicId,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);
