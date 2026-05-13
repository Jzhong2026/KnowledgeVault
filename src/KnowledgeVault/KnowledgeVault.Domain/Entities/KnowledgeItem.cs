using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItem : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid? CategoryId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string? SourceUrl { get; set; }

    public KnowledgeItemStatus Status { get; set; } = KnowledgeItemStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public User? User { get; set; }

    public Category? Category { get; set; }

    public ICollection<KnowledgeItemTag> KnowledgeItemTags { get; set; } = [];
}
