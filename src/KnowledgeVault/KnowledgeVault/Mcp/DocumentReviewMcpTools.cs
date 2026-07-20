using System.ComponentModel;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class DocumentReviewMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("Request review of a fixed document revision from one or more project members.")]
    public Task<string> RequestDocumentReview(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number to review")] int revisionNumber,
        [Description("Project member user ids (Guid values)")] string[] reviewerUserIds,
        [Description("Optional instructions for reviewers")] string? message = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentReviewProvider>();
            var reviews = await provider.CreateAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                new CreateDocumentReviewRequest(
                    revisionNumber,
                    reviewerUserIds.Select(x => McpArguments.Guid(x, nameof(reviewerUserIds))).ToArray(),
                    message),
                cancellationToken);
            return McpJson.Serialize(reviews);
        });
    }

    [McpServerTool]
    [Description("List document review assignments or requests visible to the API key owner.")]
    public Task<string> ListDocumentReviews(
        [Description("Optional project id (Guid)")] string? projectId = null,
        [Description("Optional document id (Guid)")] string? documentId = null,
        [Description("Optional status: Pending, Approved, ChangesRequested, or Cancelled")] string? status = null,
        [Description("Only reviews assigned to the API key owner")] bool assignedToMe = true,
        [Description("Only reviews requested by the API key owner")] bool requestedByMe = false,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentReviewProvider>();
            var reviews = await provider.ListAsync(
                new DocumentReviewQuery(
                    McpArguments.OptionalGuid(projectId, nameof(projectId)),
                    McpArguments.OptionalGuid(documentId, nameof(documentId)),
                    McpArguments.OptionalEnum<DocumentReviewStatus>(status, nameof(status)),
                    assignedToMe,
                    requestedByMe,
                    page,
                    pageSize),
                cancellationToken);
            return McpJson.Serialize(reviews);
        });
    }

    [McpServerTool]
    [Description("Get a review-ready bundle containing the target revision, previous revision, comments, and review assignments.")]
    public Task<string> GetDocumentReviewContext(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number to review")] int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentReviewProvider>();
            var context = await provider.GetContextAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            return McpJson.Serialize(context);
        });
    }

    [McpServerTool]
    [Description("Approve a review assignment or request changes. ChangesRequested requires a decision comment.")]
    public Task<string> SubmitDocumentReview(
        [Description("Review id (Guid)")] string reviewId,
        [Description("Decision: Approved or ChangesRequested")] string decision,
        [Description("Optional decision comment; required for ChangesRequested")] string? comment = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentReviewProvider>();
            var review = await provider.DecideAsync(
                McpArguments.Guid(reviewId, nameof(reviewId)),
                new DecideDocumentReviewRequest(
                    McpArguments.Enum<DocumentReviewDecision>(decision, nameof(decision)),
                    comment),
                cancellationToken);
            return McpJson.Serialize(review);
        });
    }

    [McpServerTool]
    [Description("Cancel a pending review request. The requester or a document editor may cancel it.")]
    public Task<string> CancelDocumentReview(
        [Description("Review id (Guid)")] string reviewId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentReviewProvider>();
            var review = await provider.CancelAsync(
                McpArguments.Guid(reviewId, nameof(reviewId)),
                cancellationToken);
            return McpJson.Serialize(review);
        });
    }
}
