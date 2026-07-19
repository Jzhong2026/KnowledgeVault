using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Reviews;

public sealed record DocumentReviewDto(
    Guid Id,
    Guid DocumentId,
    Guid RevisionId,
    int RevisionNumber,
    bool IsCurrentRevision,
    DocumentReviewStatus Status,
    Guid RequestedByUserId,
    string RequestedByDisplayName,
    Guid ReviewerUserId,
    string ReviewerDisplayName,
    string? RequestMessage,
    string? DecisionComment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReviewedAt);

public sealed record DocumentReviewContextDto(
    KnowledgeItemDto Document,
    RevisionDto Revision,
    RevisionDto? PreviousRevision,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<DocumentReviewDto> Reviews);
