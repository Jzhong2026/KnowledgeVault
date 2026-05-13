using KnowledgeVault.Contracts.Lookups;

namespace KnowledgeVault.Contracts.Providers;

public interface ILookupProvider
{
    IReadOnlyList<LookupItemDto> GetKnowledgeItemStatuses();
}
