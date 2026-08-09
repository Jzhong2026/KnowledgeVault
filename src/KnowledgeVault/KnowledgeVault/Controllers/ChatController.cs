using System.Text.Json;
using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Providers.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

/// <summary>
/// Chatbot API. The non-streaming <c>POST /messages</c> returns a complete
/// answer; the streaming variant uses Server-Sent Events for the live UI.
/// </summary>
[Authorize]
[ApiController]
[Route("api/chat")]
public sealed class ChatController(
    IChatService chat,
    IReindexService reindex,
    ICurrentUserContext currentUser,
    IProjectProvider projects) : ControllerBase
{
    /// <summary>Non-streaming answer (used by MCP and the test harness).</summary>
    [HttpPost("messages")]
    public async Task<ActionResult<ChatAnswer>> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeAsync(request.ProjectId, cancellationToken);
        var answer = await chat.AskAsync(request, scope, cancellationToken);
        return Ok(answer);
    }

    /// <summary>Streaming answer via SSE.</summary>
    [HttpPost("messages/stream")]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        Response.StatusCode = 200;
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var scope = await BuildScopeAsync(request.ProjectId, cancellationToken);
        await foreach (var ev in chat.StreamAsync(request, scope, cancellationToken))
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(ev, ev.GetType());
            await Response.Body.WriteAsync(json, cancellationToken);
            await Response.Body.WriteAsync(new byte[] { (byte)'\n' }, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("admin/reindex/status")]
    public async Task<ActionResult<ReindexStatus>> ReindexStatus(CancellationToken cancellationToken)
        => Ok(await reindex.GetStatusAsync(cancellationToken));

    [HttpPost("admin/reindex")]
    public async Task<ActionResult<ReindexStatus>> Reindex(CancellationToken cancellationToken)
    {
        if (reindex.GetStatusAsync(cancellationToken).Result.IsRunning)
        {
            return Conflict(await reindex.GetStatusAsync(cancellationToken));
        }
        // Fire-and-forget the actual work; the status endpoint reports progress.
        _ = Task.Run(() => reindex.ReindexAllAsync(cancellationToken));
        return Accepted(await reindex.GetStatusAsync(cancellationToken));
    }

    private async Task<RetrievalScope> BuildScopeAsync(Guid? projectId, CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var projectsResult = await projects.ListAsync(
            new ProjectQuery(Search: null, IncludeArchived: false, FollowingOnly: true, Page: 1, PageSize: 100), ct);
        var allowed = projectsResult.Items.Select(p => p.Id).ToArray();
        return new RetrievalScope(userId, projectId, allowed);
    }
}
