namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItemComment : AuditableEntity
{
    public Guid KnowledgeItemRevisionId { get; set; }

    public Guid AuthorUserId { get; set; }

    public Guid? ParentCommentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public KnowledgeItemRevision? Revision { get; set; }

    public User? AuthorUser { get; set; }

    public KnowledgeItemComment? ParentComment { get; set; }

    public User? ResolvedByUser { get; set; }

    public ICollection<KnowledgeItemComment> Replies { get; set; } = [];
}
