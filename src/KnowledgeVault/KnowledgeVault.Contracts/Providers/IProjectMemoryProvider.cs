using KnowledgeVault.Contracts.Documents;

namespace KnowledgeVault.Contracts.Providers;

public interface IProjectMemoryProvider
{
    Task<KnowledgeItemDto> GetAsync(Guid projectId, CancellationToken cancellationToken);

    Task<Guid> EnsureExistsAsync(Guid projectId, CancellationToken cancellationToken);

    Task<int> EnsureAllExistAsync(CancellationToken cancellationToken);
}
