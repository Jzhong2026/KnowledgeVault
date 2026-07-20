using System.ComponentModel;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class ProjectMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("List projects visible to the API key owner.")]
    public Task<string> ListProjects(
        [Description("Optional project name or description search")] string? search = null,
        [Description("Include archived projects")] bool includeArchived = false,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IProjectProvider>();
            var result = await provider.ListAsync(
                new ProjectQuery(search, includeArchived, FollowingOnly: true, page, pageSize),
                cancellationToken);
            return McpJson.Serialize(result);
        });
    }

    [McpServerTool]
    [Description("Get a project and its members when the API key owner belongs to the project.")]
    public Task<string> GetProject(
        [Description("Project id (Guid)")] string projectId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IProjectProvider>();
            var project = await provider.GetAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                cancellationToken);
            if (!project.CurrentUserRole.HasValue)
            {
                throw new KnowledgeVault.Infrastructure.Exceptions.NotFoundException("Project was not found.");
            }

            return McpJson.Serialize(project);
        });
    }

    [McpServerTool]
    [Description("List project members so a document review can be assigned to developers or agents acting for those users.")]
    public Task<string> ListProjectMembers(
        [Description("Project id (Guid)")] string projectId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IProjectProvider>();
            var members = await provider.ListMembersAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                cancellationToken);
            return McpJson.Serialize(members);
        });
    }
}
