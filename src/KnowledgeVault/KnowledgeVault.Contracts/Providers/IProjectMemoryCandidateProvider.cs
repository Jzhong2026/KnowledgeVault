using KnowledgeVault.Contracts.Projects;

namespace KnowledgeVault.Contracts.Providers;

public interface IProjectMemoryCandidateProvider
{
    Task<IReadOnlyList<ProjectMemoryCandidateDto>> ListAsync(
        Guid projectId,
        bool includeResolved,
        CancellationToken cancellationToken);

    Task<ProjectMemoryCandidateDto> CreateAsync(
        Guid projectId,
        CreateProjectMemoryCandidateRequest request,
        CancellationToken cancellationToken);

    Task<ProjectMemoryCandidateDto> AcceptAsync(
        Guid projectId,
        Guid candidateId,
        CancellationToken cancellationToken);

    Task<ProjectMemoryCandidateDto> CancelAsync(
        Guid projectId,
        Guid candidateId,
        CancellationToken cancellationToken);
}
