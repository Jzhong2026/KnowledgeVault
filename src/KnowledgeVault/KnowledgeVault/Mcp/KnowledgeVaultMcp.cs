using System.ComponentModel;
using System.Text;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

/// <summary>
/// MCP surface for searching and reading documents, listing projects, and collaborating
/// through each project's shared MEMORY.md. All operations are scoped to the authenticated user.
/// </summary>
[McpServerToolType]
[McpServerResourceType]
[McpServerPromptType]
public sealed class KnowledgeVaultMcp
{
    private readonly IServiceProvider _services;

    public KnowledgeVaultMcp(IServiceProvider services)
    {
        _services = services;
    }

    private static (KnowledgeVaultDbContext db, Guid userId) GetAuthenticatedContext(
        IServiceProvider services)
    {
        var db = services.GetRequiredService<KnowledgeVaultDbContext>();
        var user = services.GetRequiredService<ICurrentUserContext>();
        if (!user.IsAuthenticated || user.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("A signed-in user is required to use KnowledgeVault tools.");
        }

        return (db, user.UserId);
    }

    [McpServerTool]
    [Description("Search the signed-in user's knowledge documents by keyword in title or content.")]
    public async Task<string> SearchKnowledgeItems(
        [Description("Free-text search term")] string query,
        CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var (db, userId) = GetAuthenticatedContext(scope.ServiceProvider);
        var term = query.Trim();

        var items = await db.KnowledgeItems
            .Include(x => x.CurrentRevision)
            .Where(x => x.Status != KnowledgeItemStatus.Deleted &&
                ((x.Scope == DocumentScope.Personal && x.OwnerUserId == userId) ||
                 (x.Scope == DocumentScope.Project && db.ProjectMembers.Any(member =>
                     member.ProjectId == x.ProjectId && member.UserId == userId))))
            .Where(x => x.CurrentRevision != null &&
                        (x.CurrentRevision.Title.Contains(term) || x.CurrentRevision.Content.Contains(term)))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return "No matching documents were found.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Found {items.Count} document(s):");
        foreach (var item in items)
        {
            builder.AppendLine($"- [{item.Id}] {item.CurrentRevision?.Title} ({item.Scope})");
        }

        return builder.ToString();
    }

    [McpServerTool]
    [Description("Get the full content of a single knowledge document by its id.")]
    public async Task<string> GetKnowledgeItem(
        [Description("The document id (Guid)")] string id,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var documentId))
        {
            return "The provided id is not a valid Guid.";
        }

        await using var scope = _services.CreateAsyncScope();
        var (db, userId) = GetAuthenticatedContext(scope.ServiceProvider);
        var item = await db.KnowledgeItems
            .Include(x => x.CurrentRevision)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags)
            .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == documentId &&
                ((x.Scope == DocumentScope.Personal && x.OwnerUserId == userId) ||
                 (x.Scope == DocumentScope.Project && db.ProjectMembers.Any(member =>
                     member.ProjectId == x.ProjectId && member.UserId == userId))), cancellationToken);

        if (item is null)
        {
            return "Document not found or you do not have access to it.";
        }

        var revision = item.CurrentRevision;
        var builder = new StringBuilder();
        builder.AppendLine($"# {revision?.Title}");
        if (item.Category is not null)
        {
            builder.AppendLine($"Category: {item.Category.Name}");
        }

        var tags = item.KnowledgeItemTags.Where(x => x.Tag is not null).Select(x => x.Tag!.Name).ToArray();
        if (tags.Length > 0)
        {
            builder.AppendLine($"Tags: {string.Join(", ", tags)}");
        }

        builder.AppendLine();
        builder.AppendLine(revision?.Content ?? "(no content)");
        return builder.ToString();
    }

    [McpServerTool]
    [Description("List the projects the signed-in user is a member of.")]
    public async Task<string> ListProjects(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var (db, userId) = GetAuthenticatedContext(scope.ServiceProvider);
        var projects = await db.ProjectMembers
            .Where(x => x.UserId == userId)
            .Include(x => x.Project)
            .OrderBy(x => x.Project!.Name)
            .Select(x => x.Project!)
            .ToListAsync(cancellationToken);

        if (projects.Count == 0)
        {
            return "You are not a member of any projects.";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"You belong to {projects.Count} project(s):");
        foreach (var project in projects)
        {
            builder.AppendLine($"- [{project.Id}] {project.Name}");
        }

        return builder.ToString();
    }

    [McpServerTool]
    [Description("Get the shared MEMORY.md for a project the signed-in user belongs to.")]
    public async Task<string> GetProjectMemory(
        [Description("The project id (Guid)")] string projectId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return "The provided project id is not a valid Guid.";
        }

        await using var scope = _services.CreateAsyncScope();
        _ = GetAuthenticatedContext(scope.ServiceProvider);
        var provider = scope.ServiceProvider.GetRequiredService<IProjectMemoryProvider>();
        var memory = await provider.GetAsync(parsedProjectId, cancellationToken);

        return FormatProjectMemory(memory);
    }

    [McpServerTool]
    [Description("Submit a proposed Markdown addition to a section of a project's MEMORY.md. Every proposal, including an administrator's, remains pending until reviewed.")]
    public async Task<string> ProposeProjectMemoryUpdate(
        [Description("The project id (Guid)")] string projectId,
        [Description("Target section: ProjectPurpose, CurrentContext, ConstraintsAndConventions, KeyDecisions, ImportantLocationsAndCommands, AgentPrompts, AgentHandoff, or OpenQuestions")] string targetSection,
        [Description("Markdown content to append after the proposal is accepted")] string proposedContent,
        [Description("Why this belongs in shared project memory")] string? rationale,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return "The provided project id is not a valid Guid.";
        }

        if (!Enum.TryParse<ProjectMemorySection>(targetSection, true, out var parsedSection) ||
            !Enum.IsDefined(parsedSection))
        {
            return "The target memory section is invalid.";
        }

        await using var scope = _services.CreateAsyncScope();
        _ = GetAuthenticatedContext(scope.ServiceProvider);
        if (!await HasWritePermissionAsync(scope.ServiceProvider))
        {
            return "The current credential does not have documents:write permission.";
        }

        var provider = scope.ServiceProvider.GetRequiredService<IProjectMemoryCandidateProvider>();
        var candidate = await provider.CreateAsync(
            parsedProjectId,
            new CreateProjectMemoryCandidateRequest(parsedSection, proposedContent, rationale),
            cancellationToken);
        return $"Created pending memory candidate {candidate.Id} for {candidate.TargetSection}.";
    }

    [McpServerTool]
    [Description("List pending or resolved MEMORY.md candidates for a project.")]
    public async Task<string> ListProjectMemoryCandidates(
        [Description("The project id (Guid)")] string projectId,
        [Description("True to include accepted and cancelled candidates")] bool includeResolved,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId))
        {
            return "The provided project id is not a valid Guid.";
        }

        await using var scope = _services.CreateAsyncScope();
        _ = GetAuthenticatedContext(scope.ServiceProvider);
        var provider = scope.ServiceProvider.GetRequiredService<IProjectMemoryCandidateProvider>();
        var candidates = await provider.ListAsync(parsedProjectId, includeResolved, cancellationToken);
        if (candidates.Count == 0)
        {
            return "No memory candidates were found.";
        }

        var builder = new StringBuilder();
        foreach (var candidate in candidates)
        {
            builder.AppendLine(
                $"- [{candidate.Id}] {candidate.Status} / {candidate.TargetSection} / by {candidate.ProposedByDisplayName} / base revision {candidate.MemoryRevisionAtProposal}");
            builder.AppendLine(candidate.ProposedContent);
        }

        return builder.ToString();
    }

    [McpServerTool]
    [Description("Accept a pending MEMORY.md candidate. Only a project owner or administrator can accept; acceptance appends it to the target section.")]
    public async Task<string> AcceptProjectMemoryCandidate(
        [Description("The project id (Guid)")] string projectId,
        [Description("The memory candidate id (Guid)")] string candidateId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId) ||
            !Guid.TryParse(candidateId, out var parsedCandidateId))
        {
            return "The provided project or candidate id is not a valid Guid.";
        }

        await using var scope = _services.CreateAsyncScope();
        _ = GetAuthenticatedContext(scope.ServiceProvider);
        if (!await HasWritePermissionAsync(scope.ServiceProvider))
        {
            return "The current credential does not have documents:write permission.";
        }

        var provider = scope.ServiceProvider.GetRequiredService<IProjectMemoryCandidateProvider>();
        var candidate = await provider.AcceptAsync(
            parsedProjectId,
            parsedCandidateId,
            cancellationToken);
        return $"Accepted candidate {candidate.Id} into MEMORY.md revision {candidate.AppliedMemoryRevisionNumber}.";
    }

    [McpServerTool]
    [Description("Cancel a pending MEMORY.md candidate without changing the document. Only a project owner or administrator can cancel.")]
    public async Task<string> CancelProjectMemoryCandidate(
        [Description("The project id (Guid)")] string projectId,
        [Description("The memory candidate id (Guid)")] string candidateId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(projectId, out var parsedProjectId) ||
            !Guid.TryParse(candidateId, out var parsedCandidateId))
        {
            return "The provided project or candidate id is not a valid Guid.";
        }

        await using var scope = _services.CreateAsyncScope();
        _ = GetAuthenticatedContext(scope.ServiceProvider);
        if (!await HasWritePermissionAsync(scope.ServiceProvider))
        {
            return "The current credential does not have documents:write permission.";
        }

        var provider = scope.ServiceProvider.GetRequiredService<IProjectMemoryCandidateProvider>();
        var candidate = await provider.CancelAsync(
            parsedProjectId,
            parsedCandidateId,
            cancellationToken);
        return $"Cancelled memory candidate {candidate.Id}.";
    }

    [McpServerResource(UriTemplate = "knowledge://{id}")]
    [Description("Returns the full content of a knowledge document as a plain-text resource.")]
    public async Task<TextResourceContents> GetDocumentResource(
        [Description("The document id (Guid)")] string id,
        CancellationToken cancellationToken)
    {
        var content = await GetKnowledgeItem(id, cancellationToken);
        return new TextResourceContents
        {
            Uri = $"knowledge://{id}",
            MimeType = "text/plain",
            Text = content,
        };
    }

    [McpServerResource(UriTemplate = "project-memory://{projectId}")]
    [Description("Returns a project's shared MEMORY.md as a Markdown resource.")]
    public async Task<TextResourceContents> GetProjectMemoryResource(
        [Description("The project id (Guid)")] string projectId,
        CancellationToken cancellationToken)
    {
        var content = await GetProjectMemory(projectId, cancellationToken);
        return new TextResourceContents
        {
            Uri = $"project-memory://{projectId}",
            MimeType = "text/markdown",
            Text = content,
        };
    }

    [McpServerPrompt(Name = "summarize-document")]
    [Description("Build a prompt that asks the assistant to summarize a knowledge document.")]
    public async Task<GetPromptResult> SummarizeDocument(
        [Description("The document id (Guid) to summarize")] string id,
        CancellationToken cancellationToken)
    {
        var content = await GetKnowledgeItem(id, cancellationToken);
        var messages = new List<PromptMessage>
        {
            new()
            {
                Role = Role.User,
                Content = new TextContentBlock
                {
                    Text = $"Please summarize the following knowledge document (id {id}):\n\n{content}",
                },
            },
        };

        return new GetPromptResult
        {
            Description = $"Summarize document {id}",
            Messages = messages,
        };
    }

    private static async Task<bool> HasWritePermissionAsync(IServiceProvider services)
    {
        var authorizationService = services.GetRequiredService<IAuthorizationService>();
        var httpContext = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return httpContext is not null &&
            (await authorizationService.AuthorizeAsync(
                httpContext.User,
                policyName: ApiKeyScopes.DocumentsWrite)).Succeeded;
    }

    private static string FormatProjectMemory(KnowledgeItemDto memory)
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
