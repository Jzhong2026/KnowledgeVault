using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class DocumentRevisionReview : AuditableEntity
{
    public Guid KnowledgeItemRevisionId { get; set; }

    public Guid RequestedByUserId { get; set; }

    public Guid ReviewerUserId { get; set; }

    public DocumentReviewStatus Status { get; set; } = DocumentReviewStatus.Pending;

    public string? RequestMessage { get; set; }

    public string? DecisionComment { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public KnowledgeItemRevision? Revision { get; set; }

    public User? RequestedByUser { get; set; }

    public User? ReviewerUser { get; set; }
}
