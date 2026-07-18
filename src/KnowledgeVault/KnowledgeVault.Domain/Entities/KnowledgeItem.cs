using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItem : AuditableEntity
{
    public Guid OwnerUserId { get; set; }

    public DocumentScope Scope { get; set; } = DocumentScope.Personal;

    public Guid? ProjectId { get; set; }

    public Guid? TopicId { get; set; }

    public DocumentType DocumentType { get; set; } = DocumentType.General;

    public Guid? CurrentRevisionId { get; set; }

    public int CurrentRevisionNumber { get; set; }

    public Guid? CategoryId { get; set; }

    public KnowledgeItemStatus Status { get; set; } = KnowledgeItemStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public User? OwnerUser { get; set; }

    public Project? Project { get; set; }

    public ProjectTopic? Topic { get; set; }

    public Category? Category { get; set; }

    public KnowledgeItemRevision? CurrentRevision { get; set; }

    public ICollection<KnowledgeItemRevision> Revisions { get; set; } = [];

    public ICollection<KnowledgeItemTag> KnowledgeItemTags { get; set; } = [];
}
