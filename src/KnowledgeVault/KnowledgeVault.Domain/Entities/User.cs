namespace KnowledgeVault.Domain.Entities;

public sealed class User : AuditableEntity
{
    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public string? Nickname { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<KnowledgeItem> KnowledgeItems { get; set; } = [];
}
