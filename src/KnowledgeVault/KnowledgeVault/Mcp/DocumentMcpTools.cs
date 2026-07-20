using System.ComponentModel;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

[McpServerToolType]
public sealed class DocumentMcpTools(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer) : McpOperation(scopeFactory, authorizer)
{
    [McpServerTool]
    [Description("Search documents visible to the API key owner by title, summary, or content.")]
    public Task<string> SearchKnowledgeItems(
        [Description("Free-text search term")] string query,
        [Description("Optional project id (Guid)")] string? projectId = null,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var parsedProjectId = McpArguments.OptionalGuid(projectId, nameof(projectId));
            var provider = services.GetRequiredService<IDocumentProvider>();
            var result = await provider.ListAsync(
                new DocumentQuery(
                    parsedProjectId.HasValue ? DocumentScope.Project : null,
                    parsedProjectId,
                    TopicId: null,
                    DocumentType: null,
                    LinkDisplayText: null,
                    Search: query,
                    CategoryId: null,
                    OwnerUserId: null,
                    Status: null,
                    TagIds: null,
                    Sort: DocumentSort.UpdatedAtDesc,
                    Page: page,
                    PageSize: pageSize),
                cancellationToken);
            return McpJson.Serialize(result);
        });
    }

    [McpServerTool]
    [Description("List project documents with reliable project, document type, topic, status, and search filters.")]
    public Task<string> ListProjectDocuments(
        [Description("Project id (Guid)")] string projectId,
        [Description("Optional type: General, PlanningReview, TaskBreakdown, or ProjectMemory")] string? documentType = null,
        [Description("Optional project topic id (Guid)")] string? topicId = null,
        [Description("Optional status: Draft, Active, Archived, or Deleted")] string? status = null,
        [Description("Optional title, summary, or content search")] string? search = null,
        [Description("One-based page number")] int page = 1,
        [Description("Page size from 1 to 100")] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var result = await provider.ListAsync(
                new DocumentQuery(
                    DocumentScope.Project,
                    McpArguments.Guid(projectId, nameof(projectId)),
                    McpArguments.OptionalGuid(topicId, nameof(topicId)),
                    McpArguments.OptionalEnum<DocumentType>(documentType, nameof(documentType)),
                    LinkDisplayText: null,
                    Search: search,
                    CategoryId: null,
                    OwnerUserId: null,
                    McpArguments.OptionalEnum<KnowledgeItemStatus>(status, nameof(status)),
                    TagIds: null,
                    Sort: DocumentSort.UpdatedAtDesc,
                    Page: page,
                    PageSize: pageSize),
                cancellationToken);
            return McpJson.Serialize(result);
        });
    }

    [McpServerTool]
    [Description("Get the current full content and metadata of a document by id.")]
    public Task<string> GetKnowledgeItem(
        [Description("Document id (Guid)")] string id,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var document = await provider.GetAsync(McpArguments.Guid(id, nameof(id)), cancellationToken);
            return McpJson.Serialize(document);
        });
    }

    [McpServerTool]
    [Description("Create a personal or project document. Project documents are immediately visible to project members.")]
    public Task<string> CreateDocument(
        [Description("Document title")] string title,
        [Description("Markdown document content; an empty value uses the selected document type template")] string content,
        [Description("Scope: Personal or Project")] string scope = "Project",
        [Description("Type: General, PlanningReview, or TaskBreakdown")] string documentType = "General",
        [Description("Required project id for Project scope (Guid)")] string? projectId = null,
        [Description("Optional project topic id (Guid)")] string? topicId = null,
        [Description("Optional short summary")] string? summary = null,
        [Description("Optional revision change note")] string? changeNote = null,
        [Description("Initial status: Draft, Active, or Archived")] string status = "Draft",
        [Description("Optional document category id (Guid); use list_categories to discover it")] string? categoryId = null,
        [Description("Optional tag names")] string[]? tagNames = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var document = await provider.CreateAsync(
                new CreateDocumentRequest(
                    McpArguments.Enum<DocumentScope>(scope, nameof(scope)),
                    McpArguments.OptionalGuid(projectId, nameof(projectId)),
                    McpArguments.OptionalGuid(topicId, nameof(topicId)),
                    McpArguments.Enum<DocumentType>(documentType, nameof(documentType)),
                    title,
                    content,
                    summary,
                    SourceUrl: null,
                    LinkDisplayText: null,
                    LinkUrl: null,
                    changeNote,
                    McpArguments.OptionalGuid(categoryId, nameof(categoryId)),
                    McpArguments.Enum<KnowledgeItemStatus>(status, nameof(status)),
                    TagIds: null,
                    tagNames),
                cancellationToken);
            return McpJson.Serialize(document);
        });
    }

    [McpServerTool]
    [Description("Create a new document revision with optimistic concurrency protection.")]
    public Task<string> UpdateDocument(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number read before making this update")] int expectedRevisionNumber,
        [Description("Complete Markdown content for the new revision")] string content,
        [Description("Optional replacement title; omit to preserve the current title")] string? title = null,
        [Description("Optional replacement summary; omit to preserve the current summary")] string? summary = null,
        [Description("Optional explanation of this revision")] string? changeNote = null,
        [Description("Optional status: Draft, Active, or Archived; omit to preserve current status")] string? status = null,
        [Description("Optional replacement category id (Guid); omit to preserve the current category")] string? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var id = McpArguments.Guid(documentId, nameof(documentId));
            var provider = services.GetRequiredService<IDocumentProvider>();
            var current = await provider.GetAsync(id, cancellationToken);
            var updated = await provider.UpdateAsync(
                id,
                new UpdateDocumentRequest(
                    expectedRevisionNumber,
                    current.ProjectId,
                    current.TopicId,
                    title ?? current.Title,
                    content,
                    summary ?? current.Summary,
                    current.SourceUrl,
                    current.LinkDisplayText,
                    current.LinkUrl,
                    changeNote,
                    McpArguments.OptionalGuid(categoryId, nameof(categoryId)) ?? current.Category?.Id,
                    McpArguments.OptionalEnum<KnowledgeItemStatus>(status, nameof(status)) ?? current.Status,
                    current.Tags.Select(x => x.Id).ToArray(),
                    TagNames: null),
                cancellationToken);
            return McpJson.Serialize(updated);
        });
    }
}
