using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Projects;

public sealed record ProjectMemoryCandidateDto(
    Guid Id,
    Guid ProjectId,
    ProjectMemorySection TargetSection,
    string ProposedContent,
    string? Rationale,
    ProjectMemoryCandidateStatus Status,
    Guid ProposedByUserId,
    string ProposedByDisplayName,
    int MemoryRevisionAtProposal,
    Guid? ReviewedByUserId,
    string? ReviewedByDisplayName,
    DateTimeOffset? ReviewedAt,
    int? AppliedMemoryRevisionNumber,
    DateTimeOffset CreatedAt);

public sealed record CreateProjectMemoryCandidateRequest(
    ProjectMemorySection TargetSection,
    string ProposedContent,
    string? Rationale);
