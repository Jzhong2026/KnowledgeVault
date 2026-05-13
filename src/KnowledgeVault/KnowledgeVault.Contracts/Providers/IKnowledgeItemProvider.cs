using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.KnowledgeItems;

namespace KnowledgeVault.Contracts.Providers;

public interface IKnowledgeItemProvider
{
    Task<PagedResult<KnowledgeItemSummaryDto>> ListAsync(KnowledgeItemQuery query, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> CreateAsync(CreateKnowledgeItemRequest request, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> UpdateAsync(Guid id, UpdateKnowledgeItemRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
