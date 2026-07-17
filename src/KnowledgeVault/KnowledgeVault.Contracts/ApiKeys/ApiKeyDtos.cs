namespace KnowledgeVault.Contracts.ApiKeys;

public sealed record ApiKeyDto(
    Guid Id,
    string Name,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    bool IsRevoked);

public sealed record CreateApiKeyRequest(
    string Name,
    IReadOnlyList<string> Scopes,
    int? ExpiresInDays);

public sealed record ApiKeyCreatedDto(
    Guid Id,
    string Name,
    string Key,
    string Prefix,
    DateTimeOffset ExpiresAt);
