using KnowledgeVault.Contracts.Lookups;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Providers;

public sealed class LookupProvider : ILookupProvider
{
    public IReadOnlyList<LookupItemDto> GetKnowledgeItemStatuses()
    {
        return Enum.GetValues<KnowledgeItemStatus>()
            .Select(x => new LookupItemDto(x.ToString(), (int)x))
            .ToArray();
    }
}
