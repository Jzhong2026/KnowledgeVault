using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class ProjectMemoryCandidate : AuditableEntity
{
    public Guid ProjectId { get; set; }

    public ProjectMemorySection TargetSection { get; set; }

    public string ProposedContent { get; set; } = string.Empty;

    public string? Rationale { get; set; }

    public ProjectMemoryCandidateStatus Status { get; set; } = ProjectMemoryCandidateStatus.Pending;

    public Guid ProposedByUserId { get; set; }

    public int MemoryRevisionAtProposal { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public int? AppliedMemoryRevisionNumber { get; set; }

    public Project? Project { get; set; }

    public User? ProposedByUser { get; set; }

    public User? ReviewedByUser { get; set; }
}
