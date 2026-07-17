namespace KnowledgeVault.Domain.Entities;

public sealed class ApiKey : AuditableEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public string SecretHash { get; set; } = string.Empty;

    public string Scopes { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public User? User { get; set; }
}
