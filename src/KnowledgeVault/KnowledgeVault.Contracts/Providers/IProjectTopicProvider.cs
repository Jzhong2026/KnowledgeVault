using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;

namespace KnowledgeVault.Contracts.Providers;

public interface IProjectTopicProvider
{
    Task<PagedResult<ProjectTopicDto>> ListAsync(Guid projectId, ProjectTopicQuery query, CancellationToken cancellationToken);

    Task<ProjectTopicDto> GetAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken);

    Task<ProjectTopicDto> CreateAsync(Guid projectId, CreateProjectTopicRequest request, CancellationToken cancellationToken);

    Task<ProjectTopicDto> UpdateAsync(Guid projectId, Guid topicId, UpdateProjectTopicRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken);
}
