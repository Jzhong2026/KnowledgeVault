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
    [Description("Search accessible documents by title, summary, or body; metadata only.")]
    public Task<string> SearchKnowledgeItems(
        [Description("Search text")] string query,
        [Description("Optional project Guid")] string? projectId = null,
        [Description("1-based page")] int page = 1,
        [Description("Page size 1-100")] int pageSize = 20,
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
    [Description("List project documents with optional type, topic, status, and text filters; metadata only.")]
    public Task<string> ListProjectDocuments(
        [Description("Project Guid")] string projectId,
        [Description("Optional: General, PlanningReview, TaskBreakdown, ProjectMemory")] string? documentType = null,
        [Description("Optional topic Guid")] string? topicId = null,
        [Description("Optional: Draft, Active, Archived, Deleted")] string? status = null,
        [Description("Optional title/summary/body text")] string? search = null,
        [Description("1-based page")] int page = 1,
        [Description("Page size 1-100")] int pageSize = 20,
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
    [Description("Get document head, hash, and outline; never returns the body.")]
    public Task<string> GetKnowledgeItem(
        [Description("Document Guid")] string id,
        [Description("Optional revision; default current")] int? revisionNumber = null,
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
    [Description("Get heading ranges for a document.")]
    public Task<string> GetDocumentOutline(
        [Description("Document Guid")] string documentId,
        [Description("Optional revision; default current")] int? revisionNumber = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteReadAsync(async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var head = await provider.GetMcpHeadAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                revisionNumber,
                cancellationToken);
            return McpJson.Serialize(new
            {
                headings = head.Outline,
                truncated = head.OutlineTruncated
            });
        });
    }

    [McpServerTool]
    [Description("Read one bounded Markdown slice by heading, lines, or offset (max 24000 chars).")]
    public Task<string> GetDocumentContentRange(
        [Description("Document Guid")] string documentId,
        [Description("Optional revision; default current")] int? revisionNumber = null,
        [Description("ATX heading text without #")] string? heading = null,
        [Description("1-based duplicate occurrence")] int? occurrence = null,
        [Description("1-based start line")] int? startLine = null,
        [Description("Lines to read with startLine")] int? lineCount = null,
        [Description("0-based character offset")] int? offset = null,
        [Description("Characters to read with offset; max 24000")] int? limit = null,
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
    [Description("Find text or regex in one document; max 20 clipped hits with context.")]
    public Task<string> SearchInDocument(
        [Description("Document Guid")] string documentId,
        [Description("Text or .NET regex when isRegex=true")] string pattern,
        [Description("Use regex matching")] bool isRegex = false,
        [Description("Context lines 0-8")] int contextLines = 2,
        [Description("Optional revision; default current")] int? revisionNumber = null,
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
    [Description("List descendant folders and documents; metadata only.")]
    public Task<string> ListFolderContents(
        [Description("Root folder Guid; root is excluded")] string folderId,
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
    [Description("Create a document; returns metadata and hash, not the body.")]
    public Task<string> CreateDocument(
        [Description("Document title")] string title,
        [Description("Inline Markdown body, not a file path")] string content,
        [Description("Personal or Project")] string scope = "Project",
        [Description("General, PlanningReview, or TaskBreakdown")] string documentType = "General",
        [Description("Project Guid when scope=Project")] string? projectId = null,
        [Description("Optional topic Guid")] string? topicId = null,
        [Description("Optional summary")] string? summary = null,
        [Description("Optional change note")] string? changeNote = null,
        [Description("Draft, Active, or Archived")] string status = "Draft",
        [Description("Optional category Guid")] string? categoryId = null,
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
    [Description("Replace body or metadata. Omit content to keep body; prefer apply_document_patch.")]
    public Task<string> UpdateDocument(
        [Description("Document Guid")] string documentId,
        [Description("Revision read before update")] int expectedRevisionNumber,
        [Description("Optional full Markdown body; omit to keep current")] string? content = null,
        [Description("Optional title")] string? title = null,
        [Description("Optional summary")] string? summary = null,
        [Description("Optional change note")] string? changeNote = null,
        [Description("Optional status: Draft, Active, Archived")] string? status = null,
        [Description("Optional category Guid")] string? categoryId = null,
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
    [Description("Apply exact search/replace hunks atomically in one revision.")]
    public Task<string> ApplyDocumentPatch(
        [Description("Document Guid")] string documentId,
        [Description("Revision read before update")] int expectedRevisionNumber,
        [Description("Exact old text per hunk")] string[] oldTexts,
        [Description("New text per hunk")] string[] newTexts,
        [Description("Replace every match; default false. Fails if oldText matches more than once.")] bool replaceAll = false,
        [Description("Optional per-hunk replaceAll flags")] bool[]? replaceAllFlags = null,
        [Description("Optional change note")] string? changeNote = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var provider = services.GetRequiredService<IDocumentProvider>();
            var ack = await provider.ApplyPatchAsync(
                McpArguments.Guid(documentId, nameof(documentId)),
                new ApplyDocumentPatchRequest(
                    expectedRevisionNumber,
                    DocumentMcpBinding.BindPatchHunks(oldTexts, newTexts, replaceAll, replaceAllFlags),
                    changeNote),
                cancellationToken);
            return McpJson.Serialize(ack);
        });
    }

    [McpServerTool]
    [Description("Update status, category, tags, topic, or folder without a revision or body.")]
    public Task<string> UpdateDocumentMetadata(
        [Description("Document Guid")] string documentId,
        [Description("Optional: Draft, Active, Archived")] string? status = null,
        [Description("Optional category Guid; empty clears")] string? categoryId = null,
        [Description("Optional topic Guid; empty clears")] string? topicId = null,
        [Description("Optional folder Guid; empty means root")] string? folderId = null,
        [Description("Optional tag names; omit to keep")] string[]? tagNames = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(ApiKeyScopes.DocumentsWrite, async services =>
        {
            var id = McpArguments.Guid(documentId, nameof(documentId));
            var provider = services.GetRequiredService<IDocumentProvider>();
            await provider.UpdateMetadataAsync(
                id,
                DocumentMcpBinding.BindMetadata(status, categoryId, topicId, folderId, tagNames),
                cancellationToken);
            var ack = await provider.GetWriteAckAsync(id, cancellationToken);
            return McpJson.Serialize(ack);
        });
    }

    [McpServerTool]
    [Description("Move a document to a folder or the workspace root; metadata only.")]
    public Task<string> MoveDocument(
        [Description("Document Guid")] string documentId,
        [Description("Target folder Guid; null/empty means root")] string? folderId = null,
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
