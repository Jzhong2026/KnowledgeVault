namespace KnowledgeVault.Contracts.Auth;

public sealed record RegisterRequest(string UserName, string Email, string Password);

public sealed record LoginRequest(string UserNameOrEmail, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    UserProfileDto User);

public sealed record UserProfileDto(
    Guid Id,
    string UserName,
    string Email,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);
