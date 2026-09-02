using System.ComponentModel;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class RevisionMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("List a document's revision history, newest first. Metadata only.")]
    public Task<string> ListDocumentRevisions(
        [Description("Document id (Guid)")] string documentId,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IRevisionProvider>();
            var revisions = await provider.ListAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                page,
                pageSize,
                cancellationToken);
            return McpJson.Serialize(revisions);
        });
    }

    [McpServerTool]
    [Description("Get metadata and heading outline for a specific revision. Does not return the body; use get_document_content_range with revisionNumber.")]
    public Task<string> GetDocumentRevision(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number")] int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            return McpJson.Serialize(head);
        });
    }

    [McpServerTool]
    [Description("Return a unified diff between two revisions. Does not include unchanged bodies.")]
    public Task<string> GetRevisionDiff(
        [Description("Document id (Guid)")] string documentId,
        [Description("Starting revision number")] int fromRevision,
        [Description("Ending revision number")] int toRevision,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IRevisionProvider>();
            var diff = await provider.GetDiffAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                fromRevision,
                toRevision,
                cancellationToken);
            return McpDocumentFormat.Diff(diff);
        });
    }
}
