using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.KnowledgeItems;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/knowledge-items")]
public sealed class KnowledgeItemsController(IKnowledgeItemProvider knowledgeItemProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<KnowledgeItemSummaryDto>>> List(
        [FromQuery] KnowledgeItemQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await knowledgeItemProvider.ListAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeItemDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await knowledgeItemProvider.GetAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<KnowledgeItemDto>> Create(
        CreateKnowledgeItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await knowledgeItemProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeItemDto>> Update(
        Guid id,
        UpdateKnowledgeItemRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await knowledgeItemProvider.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await knowledgeItemProvider.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
