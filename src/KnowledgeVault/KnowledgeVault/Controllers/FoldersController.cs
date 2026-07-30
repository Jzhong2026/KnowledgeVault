using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text;

namespace KnowledgeVault.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/folders")]
public sealed class FoldersController(
    IFolderProvider folderProvider,
    IDocumentProvider documentProvider) : ControllerBase
{
    [Authorize(Policy = "documents:read")]
    [HttpGet]
    public async Task<ActionResult> List(
        [FromQuery] DocumentScope? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? parentFolderId,
        [FromQuery] Guid? rootFolderId,
        [FromQuery] bool includeArchived,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        // When pagination params are supplied, return the paged shape so the
        // workspace "Load more" UI can fetch incremental pages. Otherwise
        // return the full unpaged DTO (preserves existing behaviour for any
        // caller that doesn't paginate yet).
        if (page.HasValue)
        {
            var paged = await folderProvider.GetContentPagedAsync(
                scope, projectId, parentFolderId, rootFolderId, includeArchived,
                page.Value, pageSize ?? 20, cancellationToken);
            return Ok(paged);
        }
        var content = await folderProvider.GetContentAsync(scope, projectId, parentFolderId, rootFolderId, includeArchived, cancellationToken);
        return Ok(content);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("tree")]
    public async Task<ActionResult<FolderTreeNodeDto>> Tree(
        [FromQuery] DocumentScope? scope,
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? rootFolderId,
        CancellationToken cancellationToken)
    {
        var tree = await folderProvider.GetTreeAsync(scope, projectId, rootFolderId, cancellationToken);
        return Ok(tree);
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FolderSummaryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await folderProvider.GetAsync(id, cancellationToken));
    }

    [Authorize(Policy = "documents:read")]
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var root = await folderProvider.GetAsync(id, cancellationToken);
        var folderName = SanitizePathSegment(root.Name);
        var entries = new List<(string Path, string Content)>();

        var queue = new Queue<(Guid FolderId, string RelativePath)>();
        queue.Enqueue((root.Id, string.Empty));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var content = await folderProvider.GetContentAsync(
                root.Scope,
                root.ProjectId,
                current.FolderId,
                null,
                true,
                cancellationToken);

            foreach (var documentSummary in content.Documents)
            {
                var document = await documentProvider.GetAsync(documentSummary.Id, cancellationToken);
                var docName = BuildMarkdownFileName(document.Title, document.Id);
                var relativePath = string.IsNullOrEmpty(current.RelativePath)
                    ? docName
                    : $"{current.RelativePath}/{docName}";
                entries.Add((relativePath, document.Content ?? string.Empty));
            }

            foreach (var child in content.Folders)
            {
                var segment = SanitizePathSegment(child.Name);
                var childRelativePath = string.IsNullOrEmpty(current.RelativePath)
                    ? segment
                    : $"{current.RelativePath}/{segment}";
                queue.Enqueue((child.Id, childRelativePath));
            }
        }

        var archiveName = $"{folderName}.zip";
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var uniquePath = EnsureUniquePath(entry.Path, usedPaths);
                var zipEntry = zip.CreateEntry(uniquePath, CompressionLevel.Fastest);
                await using var entryStream = zipEntry.Open();
                await using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
                await writer.WriteAsync(entry.Content);
            }
        }

        stream.Position = 0;
        return File(stream, "application/zip", archiveName);
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost]
    public async Task<ActionResult<FolderSummaryDto>> Create(
        CreateFolderRequest request,
        CancellationToken cancellationToken)
    {
        var folder = await folderProvider.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = folder.Id }, folder);
    }

    [Authorize(Policy = "documents:write")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FolderSummaryDto>> Update(
        Guid id,
        UpdateFolderRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await folderProvider.UpdateAsync(id, request, cancellationToken));
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await folderProvider.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }

    [Authorize(Policy = "documents:write")]
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        await folderProvider.RestoreAsync(id, cancellationToken);
        return NoContent();
    }

    private static string BuildMarkdownFileName(string? title, Guid fallbackId)
    {
        var baseName = string.IsNullOrWhiteSpace(title)
            ? $"document-{fallbackId:D}"
            : title;
        return $"{SanitizePathSegment(baseName)}.md";
    }

    private static string SanitizePathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "untitled";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim()
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "untitled" : sanitized;
    }

    private static string EnsureUniquePath(string relativePath, ISet<string> usedPaths)
    {
        if (usedPaths.Add(relativePath))
        {
            return relativePath;
        }

        var extension = Path.GetExtension(relativePath);
        var withoutExtension = relativePath[..^extension.Length];
        var index = 2;

        while (true)
        {
            var candidate = $"{withoutExtension} ({index}){extension}";
            if (usedPaths.Add(candidate))
            {
                return candidate;
            }

            index++;
        }
    }
}
