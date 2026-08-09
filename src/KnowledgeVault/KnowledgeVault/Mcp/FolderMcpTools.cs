using System.ComponentModel;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class FolderMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("Create a personal or project folder under an optional parent folder. Project folders are immediately visible to project members.")]
    public Task<string> CreateFolder(
        [Description("Folder name (1-128 chars)")] string name,
        [Description("Scope: Personal or Project")] string scope = "Project",
        [Description("Required project id for Project scope (Guid)")] string? projectId = null,
        [Description("Optional parent folder id (Guid) for nesting")] string? parentFolderId = null,
        [Description("Optional description (max 512 chars)")] string? description = null,
        [Description("Optional sort order; lower numbers appear first")] int sortOrder = 0,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IFolderProvider>();
            var folder = await provider.CreateAsync(
                new CreateFolderRequest(
                    McpArguments.Enum<DocumentScope>(scope, nameof(scope)),
                    McpArguments.OptionalGuid(projectId, nameof(projectId)),
                    McpArguments.OptionalGuid(parentFolderId, nameof(parentFolderId)),
                    name,
                    description,
                    sortOrder),
                cancellationToken);
            return McpJson.Serialize(folder);
        });
    }
}
