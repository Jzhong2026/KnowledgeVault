using System.ComponentModel;
using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class CommentMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("List comments and replies attached to a specific document revision.")]
    public Task<string> ListRevisionComments(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<ICommentProvider>();
            var comments = await provider.ListAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                page,
                pageSize,
                cancellationToken);
            return McpJson.Serialize(comments);
        });
    }

    [McpServerTool]
    [Description("Add a top-level comment to a document revision.")]
    public Task<string> AddRevisionComment(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        [Description("Comment content")] string content,
        CancellationToken cancellationToken = default)
    {
        return AddCommentAsync(documentId, revisionNumber, content, parentCommentId: null, cancellationToken);
    }

    [McpServerTool]
    [Description("Reply to an existing comment on the same document revision.")]
    public Task<string> ReplyToRevisionComment(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        [Description("Parent comment id (Guid)")] string parentCommentId,
        [Description("Reply content")] string content,
        CancellationToken cancellationToken = default)
    {
        return AddCommentAsync(
            documentId,
            revisionNumber,
            content,
            McpArguments.Guid(parentCommentId, nameof(parentCommentId)),
            cancellationToken);
    }

    [McpServerTool]
    [Description("Edit a comment authored by the API key owner.")]
    public Task<string> UpdateRevisionComment(
        [Description("Comment id (Guid)")] string commentId,
        [Description("Replacement comment content")] string content,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.CommentsWrite, async services =>
        {
            var provider = services.GetRequiredService<ICommentProvider>();
            var comment = await provider.UpdateAsync(
                McpArguments.Guid(commentId, nameof(commentId)),
                new UpdateCommentRequest(content),
                cancellationToken);
            return McpJson.Serialize(comment);
        });
    }

    [McpServerTool]
    [Description("Resolve or reopen a revision comment. The author and document editors may perform this action.")]
    public Task<string> ResolveRevisionComment(
        [Description("Comment id (Guid)")] string commentId,
        [Description("True to resolve; false to reopen")] bool isResolved,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.CommentsWrite, async services =>
        {
            var provider = services.GetRequiredService<ICommentProvider>();
            var comment = await provider.ResolveAsync(
                McpArguments.Guid(commentId, nameof(commentId)),
                new ResolveCommentRequest(isResolved),
                cancellationToken);
            return McpJson.Serialize(comment);
        });
    }

    [McpServerTool]
    [Description("Delete a comment authored by the API key owner.")]
    public Task<string> DeleteRevisionComment(
        [Description("Comment id (Guid)")] string commentId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.CommentsWrite, async services =>
        {
            var id = McpArguments.Guid(commentId, nameof(commentId));
            var provider = services.GetRequiredService<ICommentProvider>();
            await provider.DeleteAsync(id, cancellationToken);
            return McpJson.Serialize(new { commentId = id, deleted = true });
        });
    }

    private Task<string> AddCommentAsync(
        string documentId,
        int revisionNumber,
        string content,
        Guid? parentCommentId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(ApiKeyScopes.CommentsWrite, async services =>
        {
            var provider = services.GetRequiredService<ICommentProvider>();
            var comment = await provider.AddAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                new AddCommentRequest(content, parentCommentId),
                cancellationToken);
            return McpJson.Serialize(comment);
        });
    }
}
