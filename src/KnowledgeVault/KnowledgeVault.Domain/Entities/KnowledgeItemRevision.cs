namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItemRevision : AuditableEntity
{
    public Guid KnowledgeItemId { get; set; }

    public int RevisionNumber { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public string? LinkDisplayText { get; set; }

    public string? LinkUrl { get; set; }

    public string? ChangeNote { get; set; }

    public Guid CreatedByUserId { get; set; }

    public KnowledgeItem? KnowledgeItem { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<KnowledgeItemComment> Comments { get; set; } = [];

    public ICollection<DocumentRevisionReview> Reviews { get; set; } = [];
}
