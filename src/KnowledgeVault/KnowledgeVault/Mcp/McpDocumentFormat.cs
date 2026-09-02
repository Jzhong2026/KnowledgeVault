using System.Text;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Infrastructure.Text;

namespace KnowledgeVault.Api.Mcp;

internal static class McpDocumentFormat
{
    public static DocumentWriteAckDto Ack(KnowledgeItemDto item, int? appliedCount = null)
    {
        return new DocumentWriteAckDto(
            item.Id,
            item.CurrentRevisionNumber,
            item.Title,
            item.Status,
            item.Content?.Length ?? 0,
            DocumentContentHash.Sha256Hex(item.Content),
            item.ChangeNote,
            appliedCount);
    }

    public static string Range(DocumentContentRangeDto range)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Document id: {range.DocumentId}");
        builder.AppendLine($"Revision: {range.CurrentRevisionNumber}");
        builder.AppendLine($"Content hash: {range.ContentHash}");
        builder.AppendLine($"Range: lines {range.StartLine}-{range.EndLine}, chars {range.CharOffset}+{range.CharLength}");
        builder.AppendLine($"Truncated: {range.Truncated}");
        builder.AppendLine();
        builder.Append(range.Content);
        return builder.ToString();
    }

    public static string Diff(DocumentRevisionDiffDto diff)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Document id: {diff.DocumentId}");
        builder.AppendLine($"From revision: {diff.FromRevision}");
        builder.AppendLine($"To revision: {diff.ToRevision}");
        builder.AppendLine($"Truncated: {diff.Truncated}");
        builder.AppendLine($"Old lines: {diff.OldLineCount}; new lines: {diff.NewLineCount}");
        builder.AppendLine();
        builder.Append(diff.UnifiedDiff);
        return builder.ToString();
    }
}
