using KnowledgeVault.Contracts.Lookups;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/lookups")]
public sealed class LookupsController(ILookupProvider lookupProvider) : ControllerBase
{
    [HttpGet("knowledge-item-statuses")]
    public ActionResult<IReadOnlyList<LookupItemDto>> KnowledgeItemStatuses()
    {
        return Ok(lookupProvider.GetKnowledgeItemStatuses());
    }
}
