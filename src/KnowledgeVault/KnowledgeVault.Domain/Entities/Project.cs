namespace KnowledgeVault.Domain.Entities;

public sealed class Project : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid OwnerUserId { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = [];
}
