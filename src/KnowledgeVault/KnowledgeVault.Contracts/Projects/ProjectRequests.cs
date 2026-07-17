using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Projects;

public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description, bool? IsArchived);

public sealed record AddProjectMemberRequest(Guid UserId, ProjectRole Role);

public sealed record UpdateProjectMemberRequest(ProjectRole Role);

public sealed record ProjectQuery(
    string? Search,
    bool IncludeArchived = false,
    bool FollowingOnly = false,
    int Page = 1,
    int PageSize = 20);
