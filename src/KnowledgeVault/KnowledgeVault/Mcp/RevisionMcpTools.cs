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
    [Description("List a document's revision history, newest first.")]
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
    [Description("Get the full content and metadata of a specific document revision.")]
    public Task<string> GetDocumentRevision(
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
            return McpJson.Serialize(revision);
        });
    }
}
