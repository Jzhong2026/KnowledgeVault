using System.ComponentModel;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerPromptType]
public sealed class KnowledgeVaultMcpPrompts(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerPrompt(Name = "summarize-document")]
    [Description("Build a prompt that asks the assistant to summarize a knowledge document.")]
    public Task<GetPromptResult> SummarizeDocument(
        [Description("Document id (Guid) to summarize")] string id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsRead, async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var document = await provider.GetAsync(McpArguments.Guid(id, nameof(id)), cancellationToken);
            return new GetPromptResult
            {
                Description = $"Summarize document {id}",
                Messages =
                [
                    new PromptMessage
                    {
                        Role = Role.User,
                        Content = new TextContentBlock
                        {
                            Text = $"Please summarize the following knowledge document (id {id}):\n\n{document.Content}"
                        }
                    }
                ]
            };
        });
    }
}
