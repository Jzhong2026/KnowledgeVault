namespace KnowledgeVault.Infrastructure.Auth;

public sealed record JwtTokenResult(string AccessToken, DateTimeOffset ExpiresAt);
