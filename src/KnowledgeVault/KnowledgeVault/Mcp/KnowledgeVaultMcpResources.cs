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
    [Description("Return document metadata and heading outline. Use get_document_content_range to read the body.")]
    public Task<TextResourceContents> GetDocumentResource(
        [Description("Document id (Guid)")] string id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(McpArguments.Guid(id, nameof(id)), null, cancellationToken);
            var outline = string.Join(
                "\n",
                head.Outline.Select(h =>
                    h.Level == 0
                        ? $"(no headings; {head.ContentLength} chars — use get_document_content_range)"
                        : $"{new string('#', Math.Max(h.Level, 1))} {h.Heading}  lines {h.StartLine}-{h.EndLine}"));
            return new TextResourceContents
            {
                Uri = $"knowledge://{id}",
                MimeType = "text/markdown",
                Text = $"# {head.Title}\n\nDocument id: {head.Id}\nRevision: {head.CurrentRevisionNumber}\nStatus: {head.Status}\nCharacters: {head.ContentLength}\nHash: {head.ContentHash}\n\n## Outline\n\n{outline}\n"
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
    [Description("Return revision metadata and heading outline. Use get_document_content_range with revisionNumber to read the body.")]
    public Task<TextResourceContents> GetRevisionResource(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var documentProvider = services.GetRequiredService<IDocumentProvider>();
            var head = await documentProvider.GetMcpHeadAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            var outline = string.Join(
                "\n",
                head.Outline.Select(h =>
                    h.Level == 0
                        ? $"(no headings; {head.ContentLength} chars — use get_document_content_range)"
                        : $"{new string('#', Math.Max(h.Level, 1))} {h.Heading}  lines {h.StartLine}-{h.EndLine}"));
            return new TextResourceContents
            {
                Uri = $"revision://{documentId}/{revisionNumber}",
                MimeType = "text/markdown",
                Text = $"# {head.Title}\n\nDocument id: {head.Id}\nRevision: {head.CurrentRevisionNumber}\nCharacters: {head.ContentLength}\nHash: {head.ContentHash}\n\n## Outline\n\n{outline}\n"
            };
        });
    }
}
