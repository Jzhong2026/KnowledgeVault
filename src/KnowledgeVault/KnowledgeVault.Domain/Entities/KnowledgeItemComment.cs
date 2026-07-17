namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItemComment : AuditableEntity
{
    public Guid KnowledgeItemRevisionId { get; set; }

    public Guid AuthorUserId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset? DeletedAt { get; set; }

    public KnowledgeItemRevision? Revision { get; set; }

    public User? AuthorUser { get; set; }
}
