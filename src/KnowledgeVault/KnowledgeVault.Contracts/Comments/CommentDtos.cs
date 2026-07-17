namespace KnowledgeVault.Contracts.Comments;

public sealed record CommentDto(
    Guid Id,
    int RevisionNumber,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsDeleted);

public sealed record AddCommentRequest(string Content);

public sealed record UpdateCommentRequest(string Content);
