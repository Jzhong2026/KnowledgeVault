namespace KnowledgeVault.Domain.Entities;

public sealed class Tag : AuditableEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Color { get; set; }

    public User? User { get; set; }

    public ICollection<KnowledgeItemTag> KnowledgeItemTags { get; set; } = [];
}
