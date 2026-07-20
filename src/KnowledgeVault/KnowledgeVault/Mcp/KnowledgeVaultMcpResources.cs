using System.ComponentModel;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerResourceType]
public sealed class KnowledgeVaultMcpResources(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerResource(UriTemplate = "knowledge://{id}")]
    [Description("Return the current full content of a knowledge document as Markdown.")]
    public Task<TextResourceContents> GetDocumentResource(
        [Description("Document id (Guid)")] string id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var document = await provider.GetAsync(McpArguments.Guid(id, nameof(id)), cancellationToken);
            return new TextResourceContents
            {
                Uri = $"knowledge://{id}",
                MimeType = "text/markdown",
                Text = document.Content
            };
        });
    }

    [McpServerResource(UriTemplate = "project-memory://{projectId}")]
    [Description("Return a project's shared MEMORY.md as Markdown.")]
    public Task<TextResourceContents> GetProjectMemoryResource(
        [Description("Project id (Guid)")] string projectId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryProvider>();
            var memory = await provider.GetAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                cancellationToken);
            return new TextResourceContents
            {
                Uri = $"project-memory://{projectId}",
                MimeType = "text/markdown",
                Text = memory.Content
            };
        });
    }

    [McpServerResource(UriTemplate = "revision://{documentId}/{revisionNumber}")]
    [Description("Return a specific document revision as Markdown.")]
    public Task<TextResourceContents> GetRevisionResource(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IRevisionProvider>();
            var revision = await provider.GetAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            return new TextResourceContents
            {
                Uri = $"revision://{documentId}/{revisionNumber}",
                MimeType = "text/markdown",
                Text = revision.Content
            };
        });
    }
}
