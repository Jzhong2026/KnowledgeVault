using KnowledgeVault.Contracts.Tags;

namespace KnowledgeVault.Contracts.Providers;

public interface ITagProvider
{
    Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken cancellationToken);

    Task<TagDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken);

    Task<TagDto> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
