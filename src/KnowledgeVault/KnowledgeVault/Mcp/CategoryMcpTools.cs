using System.ComponentModel;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class CategoryMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("List document categories available to the API key owner.")]
    public Task<string> ListCategories(
        [Description("Include archived categories")] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsRead, async services =>
        {
            var provider = services.GetRequiredService<ICategoryProvider>();
            var categories = await provider.ListAsync(includeArchived, cancellationToken);
            return McpJson.Serialize(categories);
        });
    }
}
