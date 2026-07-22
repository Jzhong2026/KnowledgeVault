using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/folders")]
public sealed class FoldersController(
    IFolderProvider folderProvider) : ControllerBase
{
    [Authorize(Policy = "documents:read")]
    [HttpGet]
    public async Task<ActionResult<FolderContentDto>> List(
        [FromQuery] DocumentScope? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? parentFolderId,
        [FromQuery] Guid? rootFolderId,
        CancellationToken cancellationToken)
    {
        var content = await folderProvider.GetContentAsync(scope, projectId, parentFolderId, rootFolderId, cancellationToken);
        return Ok(content);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("tree")]
    public async Task<ActionResult<FolderTreeNodeDto>> Tree(
        [FromQuery] DocumentScope? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? rootFolderId,
        CancellationToken cancellationToken)
    {
        var tree = await folderProvider.GetTreeAsync(scope, projectId, rootFolderId, cancellationToken);
        return Ok(tree);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FolderSummaryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await folderProvider.GetAsync(id, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost]
    public async Task<ActionResult<FolderSummaryDto>> Create(
        CreateFolderRequest request,
        CancellationToken cancellationToken)
    {
        var folder = await folderProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = folder.Id }, folder);
    }

    [Authorize(Policy = "documents:write")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FolderSummaryDto>> Update(
        Guid id,
        UpdateFolderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await folderProvider.UpdateAsync(id, request, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await folderProvider.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
