using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class ProjectMember : AuditableEntity
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }

    public ProjectRole Role { get; set; }

    public Project? Project { get; set; }

    public User? User { get; set; }
}
