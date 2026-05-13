namespace KnowledgeVault.Contracts.Categories;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    string? Color,
    int SortOrder,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description,
    string? Color,
    int SortOrder = 0);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    string? Color,
    int SortOrder,
    bool IsArchived);
