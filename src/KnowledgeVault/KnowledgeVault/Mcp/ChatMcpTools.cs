using System.ComponentModel;
using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Providers;
using KnowledgeVault.Providers.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class ChatMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("Ask the KnowledgeVault chatbot a natural-language question. Use this when a user asks about a story's plan, a review decision, project memory, or any other knowledge stored in the vault. Returns a textual answer plus a list of citations.")]
    public Task<string> AskKnowledgeVault(
        [Description("The user's question in natural language.")] string message,
        [Description("Optional project id (Guid) to scope the answer to a specific project.")] string? projectId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var chat = services.GetRequiredService<IChatService>();
            var currentUser = services.GetRequiredService<ICurrentUserContext>();
            var projects = services.GetRequiredService<IProjectProvider>();
            var projectsResult = await projects.ListAsync(
                new ProjectQuery(Search: null, IncludeArchived: false, FollowingOnly: true, Page: 1, PageSize: 100),
                cancellationToken);
            var allowed = projectsResult.Items.Select(p => p.Id).ToArray();
            var parsedProjectId = McpArguments.OptionalGuid(projectId, nameof(projectId));
            var scope = new RetrievalScope(currentUser.UserId, parsedProjectId, allowed);
            var answer = await chat.AskAsync(new ChatRequest(message, parsedProjectId, null), scope, cancellationToken);
            return McpJson.Serialize(answer);
        });
    }

    [McpServerTool]
    [Description("Trigger a full reindex of the chatbot's knowledge base. Walks every document, revision, review, comment, and accepted memory candidate and rebuilds the vector store. Returns the reindex status.")]
    public Task<string> ReindexKnowledgeVault(CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var reindex = services.GetRequiredService<IReindexService>();
            var status = await reindex.ReindexAllAsync(cancellationToken);
            return McpJson.Serialize(status);
        });
    }
}
