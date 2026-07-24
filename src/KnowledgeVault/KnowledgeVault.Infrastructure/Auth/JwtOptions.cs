namespace KnowledgeVault.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "KnowledgeVault";

    public string Audience { get; set; } = "KnowledgeVault";

    public string SigningKey { get; set; } = "development-only-change-this-signing-key";

    // Two weeks of persistent login by default.
    public int ExpirationMinutes { get; set; } = 14 * 24 * 60;
}
