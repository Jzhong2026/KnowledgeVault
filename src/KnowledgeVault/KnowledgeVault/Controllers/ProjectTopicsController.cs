using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:guid}/topics")]
[Route("api/projects/{projectId:guid}/groups")]
public sealed class ProjectTopicsController(IProjectTopicProvider projectTopicProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectTopicDto>>> List(
        Guid projectId,
        [FromQuery] ProjectTopicQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await projectTopicProvider.ListAsync(projectId, query, cancellationToken));
    }

    [HttpGet("{topicId:guid}")]
    public async Task<ActionResult<ProjectTopicDto>> Get(
        Guid projectId,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        return Ok(await projectTopicProvider.GetAsync(projectId, topicId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectTopicDto>> Create(
        Guid projectId,
        CreateProjectTopicRequest request,
        CancellationToken cancellationToken)
    {
        var topic = await projectTopicProvider.CreateAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId, topicId = topic.Id }, topic);
    }

    [HttpPut("{topicId:guid}")]
    public async Task<ActionResult<ProjectTopicDto>> Update(
        Guid projectId,
        Guid topicId,
        UpdateProjectTopicRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await projectTopicProvider.UpdateAsync(projectId, topicId, request, cancellationToken));
    }

    [HttpDelete("{topicId:guid}")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        await projectTopicProvider.DeleteAsync(projectId, topicId, cancellationToken);
        return NoContent();
    }
}
