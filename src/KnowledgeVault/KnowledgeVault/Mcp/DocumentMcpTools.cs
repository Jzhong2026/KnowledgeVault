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
    [Description("Search documents visible to the API key owner by title, summary, or content. Returns metadata only.")]
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
    [Description("List project documents with project, type, topic, status, and search filters. Returns metadata only.")]
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
    [Description("Get document metadata, content hash, and heading outline. Never returns the body. Use get_document_content_range or search_in_document to read text.")]
    public Task<string> GetKnowledgeItem(
        [Description("Document id (Guid)")] string id,
        [Description("Optional revision number; omit for the current revision")] int? revisionNumber = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(
                McpArguments.Guid(id, nameof(id)),
                revisionNumber,
                cancellationToken);
            return McpJson.Serialize(head);
        });
    }

    [McpServerTool]
    [Description("Get the heading outline of a document: level, heading, occurrence, line and character ranges.")]
    public Task<string> GetDocumentOutline(
        [Description("Document id (Guid)")] string documentId,
        [Description("Optional revision number; omit for the current revision")] int? revisionNumber = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            return McpJson.Serialize(head.Outline);
        });
    }

    [McpServerTool]
    [Description("Read a slice of a document as Markdown. Provide exactly one mode: heading, startLine+lineCount, or offset+limit. Max 24000 characters.")]
    public Task<string> GetDocumentContentRange(
        [Description("Document id (Guid)")] string documentId,
        [Description("Optional revision number; omit for the current revision")] int? revisionNumber = null,
        [Description("Heading text to read (ATX heading without #)")] string? heading = null,
        [Description("1-based occurrence when the heading is repeated")] int? occurrence = null,
        [Description("1-based start line")] int? startLine = null,
        [Description("Number of lines to read; required with startLine")] int? lineCount = null,
        [Description("0-based character offset")] int? offset = null,
        [Description("Character count; required with offset, max 24000")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var range = await provider.GetMcpContentRangeAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                new DocumentContentRangeQuery(heading, occurrence, startLine, lineCount, offset, limit),
                cancellationToken);
            return McpDocumentFormat.Range(range);
        });
    }

    [McpServerTool]
    [Description("Search inside one document. Returns matching lines and nearby context, not the full body. Max 20 hits.")]
    public Task<string> SearchInDocument(
        [Description("Document id (Guid)")] string documentId,
        [Description("Literal substring, or a regular expression when isRegex is true")] string pattern,
        [Description("Treat pattern as a .NET regular expression")] bool isRegex = false,
        [Description("Context lines around each hit (0-8)")] int contextLines = 2,
        [Description("Optional revision number; omit for the current revision")] int? revisionNumber = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var result = await provider.SearchInDocumentAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                new DocumentSearchQuery(pattern, isRegex, contextLines),
                cancellationToken);
            return McpJson.Serialize(result);
        });
    }

    [McpServerTool]
    [Description("Recursively list subfolders and documents below a folder. Metadata only; use get_document_content_range to read bodies.")]
    public Task<string> ListFolderContents(
        [Description("Root folder id (Guid); the root itself is not returned")] string folderId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IFolderProvider>();
            var items = await provider.ListDescendantsForMcpAsync(
                McpArguments.Guid(folderId, nameof(folderId)), cancellationToken);
            return McpJson.Serialize(items);
        });
    }

    [McpServerTool]
    [Description("Create a personal or project document. Returns metadata and a content hash, not the body.")]
    public Task<string> CreateDocument(
        [Description("Document title")] string title,
        [Description("Inline Markdown body. Pass the actual text, not a file path.")] string content,
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
            return McpJson.Serialize(McpDocumentFormat.Ack(document));
        });
    }

    [McpServerTool]
    [Description("Replace the full document or change title/summary. Omit content to keep the current body. Prefer apply_document_patch for local edits. Returns metadata only.")]
    public Task<string> UpdateDocument(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number read before making this update")] int expectedRevisionNumber,
        [Description("Optional full Markdown replacement. Omit to keep the current body. Do not pass a file path.")] string? content = null,
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
            return McpJson.Serialize(McpDocumentFormat.Ack(updated));
        });
    }

    [McpServerTool]
    [Description("Apply one or more search-replace hunks in a single new revision. Atomic: any failed hunk rolls back. Prefer this over update_document for local edits.")]
    public Task<string> ApplyDocumentPatch(
        [Description("Document id (Guid)")] string documentId,
        [Description("Revision number read before making this update")] int expectedRevisionNumber,
        [Description("Exact substrings to find, one per hunk")] string[] oldTexts,
        [Description("Replacements, same length as oldTexts")] string[] newTexts,
        [Description("When true, every hunk replaces all matches of its oldText")] bool replaceAll = false,
        [Description("Optional explanation of this revision")] string? changeNote = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            if (oldTexts is null || newTexts is null || oldTexts.Length == 0 || oldTexts.Length != newTexts.Length)
            {
                throw new KnowledgeVault.Infrastructure.Exceptions.ValidationException(
                    "oldTexts and newTexts must be non-empty arrays of the same length.");
            }

            var provider = services.GetRequiredService<IDocumentProvider>();
            var ack = await provider.ApplyPatchAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                new ApplyDocumentPatchRequest(
                    expectedRevisionNumber,
                    oldTexts.Select((oldText, index) => new DocumentPatchHunk(oldText, newTexts[index], replaceAll)).ToArray(),
                    changeNote),
                cancellationToken);
            return McpJson.Serialize(ack);
        });
    }

    [McpServerTool]
    [Description("Update document status, category, tags, topic, or folder without creating a revision or sending the body.")]
    public Task<string> UpdateDocumentMetadata(
        [Description("Document id (Guid)")] string documentId,
        [Description("Optional status: Draft, Active, or Archived")] string? status = null,
        [Description("Optional category id (Guid)")] string? categoryId = null,
        [Description("Optional project topic id (Guid)")] string? topicId = null,
        [Description("Optional tag names; omit to leave tags unchanged")] string[]? tagNames = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var id = McpArguments.Guid(documentId, nameof(documentId));
            var provider = services.GetRequiredService<IDocumentProvider>();
            var current = await provider.GetAsync(id, cancellationToken);
            await provider.UpdateMetadataAsync(
                id,
                new UpdateDocumentMetadataRequest(
                    current.ProjectId,
                    McpArguments.OptionalGuid(topicId, nameof(topicId)) ?? current.TopicId,
                    McpArguments.OptionalGuid(categoryId, nameof(categoryId)) ?? current.Category?.Id,
                    McpArguments.OptionalEnum<KnowledgeItemStatus>(status, nameof(status)) ?? current.Status,
                    tagNames is null ? current.Tags.Select(x => x.Id).ToArray() : null,
                    tagNames),
                cancellationToken);
            var ack = await provider.GetWriteAckAsync(id, cancellationToken);
            return McpJson.Serialize(ack);
        });
    }

    [McpServerTool]
    [Description("Move a document into a different folder. Pass a null folderId to move it to the workspace root. Returns metadata only.")]
    public Task<string> MoveDocument(
        [Description("Document id (Guid)")] string documentId,
        [Description("Target folder id (Guid), or null/empty to move to the root")] string? folderId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var id = McpArguments.Guid(documentId, nameof(documentId));
            await provider.MoveDocumentAsync(
                id,
                McpArguments.OptionalGuid(folderId, nameof(folderId)),
                cancellationToken);
            var ack = await provider.GetWriteAckAsync(id, cancellationToken);
            return McpJson.Serialize(ack);
        });
    }
}
