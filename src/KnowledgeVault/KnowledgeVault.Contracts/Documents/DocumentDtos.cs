using KnowledgeVault.Contracts.Categories;
using KnowledgeVault.Contracts.Tags;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Documents;

public sealed record KnowledgeItemDto(
    Guid Id,
    DocumentScope Scope,
    Guid? TopicId,
    Guid? ProjectId,
    DocumentType DocumentType,
    int CurrentRevisionNumber,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    string? TicketNo,
    string? TicketUrl,
    string? ChangeNote,
    KnowledgeItemStatus Status,
    CategoryDto? Category,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt);

public sealed record KnowledgeItemSummaryDto(
    Guid Id,
    DocumentScope Scope,
    Guid? TopicId,
    Guid? ProjectId,
    DocumentType DocumentType,
    int CurrentRevisionNumber,
    string Title,
    string? Summary,
    string? TicketNo,
    KnowledgeItemStatus Status,
    CategoryDto? Category,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record RevisionSummaryDto(
    Guid Id,
    int RevisionNumber,
    string Title,
    string? Summary,
    string? ChangeNote,
    string? TicketNo,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTimeOffset CreatedAt);

public sealed record RevisionDto(
    Guid Id,
    int RevisionNumber,
    string Title,
    string? Summary,
    string Content,
    string? SourceUrl,
    string? TicketNo,
    string? TicketUrl,
    string? ChangeNote,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTimeOffset CreatedAt);
