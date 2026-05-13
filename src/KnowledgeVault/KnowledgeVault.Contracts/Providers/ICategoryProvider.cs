using KnowledgeVault.Contracts.Categories;

namespace KnowledgeVault.Contracts.Providers;

public interface ICategoryProvider
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);

    Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
