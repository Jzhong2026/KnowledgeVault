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

// Shared shape of the optional revision content fields carried by both
// create and update requests, so revision construction can be written once.
public interface IDocumentContentRequest
{
    string? Summary { get; }
    string? SourceUrl { get; }
    string? LinkDisplayText { get; }
    string? LinkUrl { get; }
    string? ChangeNote { get; }
}

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
    Guid? FolderId = null) : IDocumentContentRequest;

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
    Guid? FolderId = null) : IDocumentContentRequest;

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
