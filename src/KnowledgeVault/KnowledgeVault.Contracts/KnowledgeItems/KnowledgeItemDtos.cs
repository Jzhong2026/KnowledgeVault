using KnowledgeVault.Contracts.Categories;
using KnowledgeVault.Contracts.Tags;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.KnowledgeItems;

public sealed record KnowledgeItemDto(
    Guid Id,
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    KnowledgeItemStatus Status,
    CategoryDto? Category,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt);

public sealed record KnowledgeItemSummaryDto(
    Guid Id,
    string Title,
    string? Summary,
    KnowledgeItemStatus Status,
    CategoryDto? Category,
    IReadOnlyList<TagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record KnowledgeItemQuery(
    string? Search,
    Guid? CategoryId,
    KnowledgeItemStatus? Status,
    IReadOnlyList<Guid>? TagIds,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateKnowledgeItemRequest(
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);

public sealed record UpdateKnowledgeItemRequest(
    string Title,
    string Content,
    string? Summary,
    string? SourceUrl,
    Guid? CategoryId,
    KnowledgeItemStatus Status,
    IReadOnlyList<Guid>? TagIds,
    IReadOnlyList<string>? TagNames);
