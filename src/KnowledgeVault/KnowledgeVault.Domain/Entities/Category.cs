namespace KnowledgeVault.Domain.Entities;

public sealed class Category : AuditableEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Color { get; set; }

    public int SortOrder { get; set; }

    public bool IsArchived { get; set; }

    public User? User { get; set; }

    public ICollection<KnowledgeItem> KnowledgeItems { get; set; } = [];
}
