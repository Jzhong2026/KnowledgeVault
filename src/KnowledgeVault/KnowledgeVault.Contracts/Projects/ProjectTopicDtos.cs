using KnowledgeVault.Contracts.Common;

namespace KnowledgeVault.Contracts.Projects;

public sealed record ProjectTopicDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Description,
    int SortOrder,
    bool IsArchived,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateProjectTopicRequest(string Name, string? Description, int SortOrder = 0);

public sealed record UpdateProjectTopicRequest(string Name, string? Description, int SortOrder, bool? IsArchived);

public sealed record ProjectTopicQuery(
    string? Search,
    bool IncludeArchived = false,
    int Page = 1,
    int PageSize = 50);
