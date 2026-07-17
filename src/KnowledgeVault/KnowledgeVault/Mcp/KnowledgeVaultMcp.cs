using System.ComponentModel;
using System.Text;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Providers;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

/// <summary>
/// MCP surface for KnowledgeVault: read-only tools to search/read documents and list
/// projects, a resource that exposes a document's content, and a prompt that helps
/// summarize a document. All operations are scoped to the authenticated user.
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

    private (KnowledgeVaultDbContext db, Guid userId) OpenScope(CancellationToken cancellationToken)
    {
        var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeVaultDbContext>();
        var user = scope.ServiceProvider.GetRequiredService<ICurrentUserContext>();
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
        var (db, userId) = OpenScope(cancellationToken);
        var term = query.Trim();

        var items = await db.KnowledgeItems
            .Include(x => x.CurrentRevision)
            .Where(x => x.Status != KnowledgeItemStatus.Deleted && x.OwnerUserId == userId)
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

        var (db, userId) = OpenScope(cancellationToken);
        var item = await db.KnowledgeItems
            .Include(x => x.CurrentRevision)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags)
            .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == documentId && x.OwnerUserId == userId, cancellationToken);

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
        var (db, userId) = OpenScope(cancellationToken);
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
}
