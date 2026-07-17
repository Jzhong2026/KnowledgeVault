using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(
    IDocumentProvider documentProvider,
    IRevisionProvider revisionProvider,
    ICommentProvider commentProvider) : ControllerBase
{
    [Authorize(Policy = "documents:read")]
    [HttpGet]
    public async Task<ActionResult<PagedResult<KnowledgeItemSummaryDto>>> List(
        [FromQuery] DocumentQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await documentProvider.ListAsync(query, cancellationToken));
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("owners")]
    public async Task<ActionResult<IReadOnlyList<DocumentOwnerDto>>> ListOwners(
        [FromQuery] Guid? projectId,
        CancellationToken cancellationToken)
    {
        return Ok(await documentProvider.ListOwnersAsync(projectId, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost]
    public async Task<ActionResult<KnowledgeItemDto>> Create(
        CreateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var document = await documentProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { documentId = document.Id }, document);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{documentId:guid}")]
    public async Task<ActionResult<KnowledgeItemDto>> Get(Guid documentId, CancellationToken cancellationToken)
    {
        return Ok(await documentProvider.GetAsync(documentId, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPut("{documentId:guid}")]
    public async Task<ActionResult<KnowledgeItemDto>> Update(
        Guid documentId,
        UpdateDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await documentProvider.UpdateAsync(documentId, request, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPatch("{documentId:guid}/metadata")]
    public async Task<ActionResult<KnowledgeItemDto>> UpdateMetadata(
        Guid documentId,
        UpdateDocumentMetadataRequest request,
        CancellationToken cancellationToken)
    {
        await documentProvider.UpdateMetadataAsync(documentId, request, cancellationToken);
        return Ok(await documentProvider.GetAsync(documentId, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken cancellationToken)
    {
        await documentProvider.DeleteAsync(documentId, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{documentId:guid}/revisions")]
    public async Task<ActionResult<PagedResult<RevisionSummaryDto>>> ListRevisions(
        Guid documentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await revisionProvider.ListAsync(documentId, page, pageSize, cancellationToken));
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{documentId:guid}/revisions/{revisionNumber:int}")]
    public async Task<ActionResult<RevisionDto>> GetRevision(
        Guid documentId,
        int revisionNumber,
        CancellationToken cancellationToken)
    {
        return Ok(await revisionProvider.GetAsync(documentId, revisionNumber, cancellationToken));
    }

    [Authorize(Policy = "comments:read")]
    [HttpGet("{documentId:guid}/revisions/{revisionNumber:int}/comments")]
    public async Task<ActionResult<PagedResult<CommentDto>>> ListComments(
        Guid documentId,
        int revisionNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await commentProvider.ListAsync(documentId, revisionNumber, page, pageSize, cancellationToken));
    }

    [Authorize(Policy = "comments:write")]
    [HttpPost("{documentId:guid}/revisions/{revisionNumber:int}/comments")]
    public async Task<ActionResult<CommentDto>> AddComment(
        Guid documentId,
        int revisionNumber,
        AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var comment = await commentProvider.AddAsync(documentId, revisionNumber, request, cancellationToken);
        return CreatedAtAction(nameof(ListComments), new { documentId, revisionNumber }, comment);
    }

    [Authorize(Policy = "comments:write")]
    [HttpPut("comments/{commentId:guid}")]
    public async Task<ActionResult<CommentDto>> UpdateComment(
        Guid commentId,
        UpdateCommentRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await commentProvider.UpdateAsync(commentId, request, cancellationToken));
    }

    [Authorize(Policy = "comments:write")]
    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken cancellationToken)
    {
        await commentProvider.DeleteAsync(commentId, cancellationToken);
        return NoContent();
    }
}
