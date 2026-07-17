using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(IProjectProvider projectProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProjectSummaryDto>>> List(
        [FromQuery] ProjectQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await projectProvider.ListAsync(query, cancellationToken));
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid projectId, CancellationToken cancellationToken)
    {
        return Ok(await projectProvider.GetAsync(projectId, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await projectProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id }, project);
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await projectProvider.UpdateAsync(projectId, request, cancellationToken));
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        await projectProvider.DeleteAsync(projectId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> ListMembers(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        return Ok(await projectProvider.ListMembersAsync(projectId, cancellationToken));
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken)
    {
        var member = await projectProvider.AddMemberAsync(projectId, request, cancellationToken);
        return CreatedAtAction(nameof(ListMembers), new { projectId }, member);
    }

    [HttpPut("{projectId:guid}/members/{userId:guid}")]
    public async Task<ActionResult<ProjectMemberDto>> UpdateMember(
        Guid projectId,
        Guid userId,
        UpdateProjectMemberRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await projectProvider.UpdateMemberAsync(projectId, userId, request, cancellationToken));
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await projectProvider.RemoveMemberAsync(projectId, userId, cancellationToken);
        return NoContent();
    }
}
