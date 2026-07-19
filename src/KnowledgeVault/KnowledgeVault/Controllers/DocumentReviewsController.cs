using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api")]
public sealed class DocumentReviewsController(IDocumentReviewProvider reviewProvider) : ControllerBase
{
    [Authorize(Policy = "documents:write")]
    [HttpPost("documents/{documentId:guid}/reviews")]
    public async Task<ActionResult<IReadOnlyList<DocumentReviewDto>>> Create(
        Guid documentId,
        CreateDocumentReviewRequest request,
        CancellationToken cancellationToken)
    {
        var reviews = await reviewProvider.CreateAsync(documentId, request, cancellationToken);
        return CreatedAtAction(nameof(GetContext), new { documentId, revisionNumber = request.RevisionNumber }, reviews);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("document-reviews")]
    public async Task<ActionResult<PagedResult<DocumentReviewDto>>> List(
        [FromQuery] DocumentReviewQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewProvider.ListAsync(query, cancellationToken));
    }

    [Authorize(Policy = "documents:read")]
    [Authorize(Policy = "comments:read")]
    [HttpGet("documents/{documentId:guid}/revisions/{revisionNumber:int}/review-context")]
    public async Task<ActionResult<DocumentReviewContextDto>> GetContext(
        Guid documentId,
        int revisionNumber,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewProvider.GetContextAsync(documentId, revisionNumber, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost("document-reviews/{reviewId:guid}/decision")]
    public async Task<ActionResult<DocumentReviewDto>> Decide(
        Guid reviewId,
        DecideDocumentReviewRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewProvider.DecideAsync(reviewId, request, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost("document-reviews/{reviewId:guid}/cancel")]
    public async Task<ActionResult<DocumentReviewDto>> Cancel(
        Guid reviewId,
        CancellationToken cancellationToken)
    {
        return Ok(await reviewProvider.CancelAsync(reviewId, cancellationToken));
    }
}
