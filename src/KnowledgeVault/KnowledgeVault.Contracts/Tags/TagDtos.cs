namespace KnowledgeVault.Contracts.Tags;

public sealed record TagDto(
    Guid Id,
    string Name,
    string? Color,
    int KnowledgeItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateTagRequest(string Name, string? Color);

public sealed record UpdateTagRequest(string Name, string? Color);
