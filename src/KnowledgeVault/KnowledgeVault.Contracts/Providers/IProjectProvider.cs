using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;

namespace KnowledgeVault.Contracts.Providers;

public interface IProjectProvider
{
    Task<PagedResult<ProjectSummaryDto>> ListAsync(ProjectQuery query, CancellationToken cancellationToken);

    Task<ProjectDto> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectDto> FollowAsync(Guid projectId, CancellationToken cancellationToken);

    Task UnfollowAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(Guid projectId, CancellationToken cancellationToken);

    Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken);

    Task<ProjectMemberDto> UpdateMemberAsync(Guid projectId, Guid userId, UpdateProjectMemberRequest request, CancellationToken cancellationToken);

    Task RemoveMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken);
}
