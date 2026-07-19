namespace KnowledgeVault.Contracts.Comments;

public sealed record CommentDto(
    Guid Id,
    int RevisionNumber,
    Guid? ParentCommentId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted,
    bool IsResolved,
    Guid? ResolvedByUserId,
    string? ResolvedByDisplayName,
    DateTimeOffset? ResolvedAt);

public sealed record AddCommentRequest(string Content, Guid? ParentCommentId = null);

public sealed record UpdateCommentRequest(string Content);

public sealed record ResolveCommentRequest(bool IsResolved);
