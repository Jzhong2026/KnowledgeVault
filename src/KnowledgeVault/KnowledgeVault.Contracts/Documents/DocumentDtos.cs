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
    string? LinkDisplayText,
    string? LinkUrl,
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
    string? ProjectName,
    string? GroupName,
    Guid OwnerUserId,
    string OwnerDisplayName,
    DocumentType DocumentType,
    int CurrentRevisionNumber,
    string Title,
    string? Summary,
    string? LinkDisplayText,
    KnowledgeItemStatus Status,
    CategoryDto? Category,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record DocumentOwnerDto(Guid Id, string DisplayName);

public sealed record DocumentActivityDayDto(DateOnly Date, int ChangeCount);

public sealed record ProjectDocumentStatsDto(int DocumentCount, int CategoryCount, int TagCount);

public sealed record RevisionSummaryDto(
    Guid Id,
    int RevisionNumber,
    string Title,
    string? Summary,
    string? ChangeNote,
    string? LinkDisplayText,
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
    string? LinkDisplayText,
    string? LinkUrl,
    string? ChangeNote,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTimeOffset CreatedAt);
