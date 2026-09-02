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
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(McpArguments.Guid(id, nameof(id)), null, cancellationToken);
            var outline = string.Join(
                Environment.NewLine,
                head.Outline.Select(h => h.Level == 0
                    ? $"(no headings, {head.ContentLength} chars)"
                    : $"{new string('#', h.Level)} {h.Heading}  lines {h.StartLine}-{h.EndLine}"));
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
                            Text = $"Summarize knowledge document {id} (revision {head.CurrentRevisionNumber}, {head.ContentLength} characters). Use get_document_content_range or search_in_document if you need body text. Outline:\n\n{outline}"
                        }
                    }
                ]
            };
        });
    }
}
