using System.ComponentModel;
using System.Text;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class ProjectMemoryMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("Get the shared MEMORY.md for a project the API key owner belongs to.")]
    public Task<string> GetProjectMemory(
        [Description("Project id (Guid)")] string projectId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsRead, async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryProvider>();
            var memory = await provider.GetAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                cancellationToken);
            return FormatMemory(memory);
        });
    }

    [McpServerTool]
    [Description("Submit a proposed Markdown addition to a section of project MEMORY.md for review.")]
    public Task<string> ProposeProjectMemoryUpdate(
        [Description("Project id (Guid)")] string projectId,
        [Description("Target section: ProjectPurpose, CurrentContext, ConstraintsAndConventions, KeyDecisions, ImportantLocationsAndCommands, AgentPrompts, AgentHandoff, or OpenQuestions")] string targetSection,
        [Description("Markdown content to append after approval")] string proposedContent,
        [Description("Optional rationale for the change")] string? rationale = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryCandidateProvider>();
            var candidate = await provider.CreateAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                new CreateProjectMemoryCandidateRequest(
                    McpArguments.Enum<ProjectMemorySection>(targetSection, nameof(targetSection)),
                    proposedContent,
                    rationale),
                cancellationToken);
            return McpJson.Serialize(candidate);
        });
    }

    [McpServerTool]
    [Description("List pending or resolved MEMORY.md change candidates for a project.")]
    public Task<string> ListProjectMemoryCandidates(
        [Description("Project id (Guid)")] string projectId,
        [Description("Include accepted and cancelled candidates")] bool includeResolved = false,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsRead, async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryCandidateProvider>();
            var candidates = await provider.ListAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                includeResolved,
                cancellationToken);
            return McpJson.Serialize(candidates);
        });
    }

    [McpServerTool]
    [Description("Accept a pending MEMORY.md candidate. Only a project owner or administrator can accept it.")]
    public Task<string> AcceptProjectMemoryCandidate(
        [Description("Project id (Guid)")] string projectId,
        [Description("Memory candidate id (Guid)")] string candidateId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryCandidateProvider>();
            var candidate = await provider.AcceptAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                McpArguments.Guid(candidateId, nameof(candidateId)),
                cancellationToken);
            return McpJson.Serialize(candidate);
        });
    }

    [McpServerTool]
    [Description("Cancel a pending MEMORY.md candidate. Only a project owner or administrator can cancel it.")]
    public Task<string> CancelProjectMemoryCandidate(
        [Description("Project id (Guid)")] string projectId,
        [Description("Memory candidate id (Guid)")] string candidateId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IProjectMemoryCandidateProvider>();
            var candidate = await provider.CancelAsync(
                McpArguments.Guid(projectId, nameof(projectId)),
                McpArguments.Guid(candidateId, nameof(candidateId)),
                cancellationToken);
            return McpJson.Serialize(candidate);
        });
    }

    internal static string FormatMemory(KnowledgeVault.Contracts.Documents.KnowledgeItemDto memory)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Document id: {memory.Id}");
        builder.AppendLine($"Revision: {memory.CurrentRevisionNumber}");
        builder.AppendLine($"Updated at: {(memory.UpdatedAt ?? memory.CreatedAt):O}");
        builder.AppendLine();
        builder.AppendLine(memory.Content);
        return builder.ToString();
    }
}
