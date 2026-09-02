using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;

namespace KnowledgeVault.Api.Mcp;

public static class DocumentMcpBinding
{
    public static IReadOnlyList<DocumentPatchHunk> BindPatchHunks(
        string[]? oldTexts,
        string[]? newTexts,
        bool replaceAll,
        bool[]? replaceAllFlags)
    {
        if (oldTexts is null || newTexts is null || oldTexts.Length == 0 || oldTexts.Length != newTexts.Length)
        {
            throw new ValidationException("oldTexts and newTexts must be non-empty arrays of the same length.");
        }

        if (replaceAllFlags is not null && replaceAllFlags.Length != oldTexts.Length)
        {
            throw new ValidationException("replaceAllFlags must be the same length as oldTexts.");
        }

        return oldTexts
            .Select((oldText, index) => new DocumentPatchHunk(
                oldText,
                newTexts[index],
                replaceAllFlags is not null ? replaceAllFlags[index] : replaceAll))
            .ToArray();
    }

    public static UpdateDocumentMetadataRequest BindMetadata(
        string? status,
        string? categoryId,
        string? topicId,
        string? folderId,
        string[]? tagNames)
    {
        return new UpdateDocumentMetadataRequest(
            ProjectId: null,
            TopicId: McpArguments.OptionalGuid(topicId, nameof(topicId)),
            CategoryId: McpArguments.OptionalGuid(categoryId, nameof(categoryId)),
            Status: McpArguments.OptionalEnum<KnowledgeItemStatus>(status, nameof(status)),
            TagIds: null,
            TagNames: tagNames,
            FolderId: folderId is null ? null : McpArguments.OptionalGuid(folderId, nameof(folderId)),
            UpdateFolder: folderId is not null,
            Patch: true,
            ClearTopic: topicId is not null && string.IsNullOrWhiteSpace(topicId),
            ClearCategory: categoryId is not null && string.IsNullOrWhiteSpace(categoryId));
    }
}
