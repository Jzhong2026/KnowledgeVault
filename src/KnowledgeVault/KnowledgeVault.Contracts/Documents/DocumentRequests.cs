using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Documents;

public sealed record DocumentQuery(
    DocumentScope? Scope,
    Guid? ProjectId,
    Guid? TopicId,
    DocumentType? DocumentType,
    string? TicketNo,
    string? Search,
    Guid? CategoryId,
    KnowledgeItemStatus? Status,
    IReadOnlyList<Guid>? TagIds,
    DocumentSort? Sort,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateDocumentRequest(
    DocumentScope Scope,
    Guid? TopicId,
    DocumentType DocumentType,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    string? TicketUrl,
    string? ChangeNote,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);

public sealed record UpdateDocumentRequest(
    int ExpectedRevisionNumber,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    string? TicketUrl,
    string? ChangeNote,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);

public sealed record UpdateDocumentMetadataRequest(
    Guid? TopicId,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);
