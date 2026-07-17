using KnowledgeVault.Contracts.ApiKeys;

namespace KnowledgeVault.Contracts.Providers;

public interface IApiKeyProvider
{
    Task<ApiKeyCreatedDto> CreateAsync(CreateApiKeyRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ApiKeyDto>> ListAsync(CancellationToken cancellationToken);

    Task RevokeAsync(Guid id, CancellationToken cancellationToken);
}
