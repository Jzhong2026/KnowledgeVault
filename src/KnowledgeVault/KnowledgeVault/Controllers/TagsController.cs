using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Tags;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tags")]
public sealed class TagsController(ITagProvider tagProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> List(CancellationToken cancellationToken)
    {
        return Ok(await tagProvider.ListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TagDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await tagProvider.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(CreateTagRequest request, CancellationToken cancellationToken)
    {
        var tag = await tagProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = tag.Id }, tag);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TagDto>> Update(Guid id, UpdateTagRequest request, CancellationToken cancellationToken)
    {
        return Ok(await tagProvider.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await tagProvider.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
