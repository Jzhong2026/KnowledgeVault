namespace KnowledgeVault.Domain.Entities;

public sealed class ProjectTopic : AuditableEntity
{
    public Guid ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsArchived { get; set; }

    public Project? Project { get; set; }
}
