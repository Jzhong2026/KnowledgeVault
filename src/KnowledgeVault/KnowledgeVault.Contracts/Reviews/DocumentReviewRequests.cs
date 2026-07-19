using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Reviews;

public sealed record CreateDocumentReviewRequest(
    int RevisionNumber,
    IReadOnlyList<Guid> ReviewerUserIds,
    string? Message);

public sealed record DocumentReviewQuery(
    Guid? ProjectId,
    Guid? DocumentId,
    DocumentReviewStatus? Status,
    bool AssignedToMe = false,
    bool RequestedByMe = false,
    int Page = 1,
    int PageSize = 20);

public sealed record DecideDocumentReviewRequest(
    DocumentReviewDecision Decision,
    string? Comment);
