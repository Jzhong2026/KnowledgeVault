using KnowledgeVault.Contracts.Chat;

namespace KnowledgeVault.Providers.AI;

public interface IReindexService
{
    /// <summary>Full rebuild — drops the collection and re-embeds every chunk.</summary>
    Task<ReindexStatus> ReindexAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the last known status without triggering a new run.</summary>
    Task<ReindexStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}
