using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Documents;

public static class DocumentMcpLimits
{
    public const int RangeMaxChars = 24_000;
    public const int SearchMaxHits = 20;
    public const int SearchContextLinesDefault = 2;
    public const int SearchContextLinesMax = 8;
    public const int SearchExcerptChars = 240;
    public const int SearchTotalChars = 4_000;
    public const int DiffContextLines = 3;
    public const int DiffMaxChars = 16_000;
    public const long DiffMaxLcsCells = 1_200_000;
    public const int MaxOutlineHeadings = 80;
}

public sealed record DocumentWriteAckDto(
    Guid DocumentId,
    int CurrentRevisionNumber,
    string Title,
    KnowledgeItemStatus Status,
    int ContentLength,
    string ContentHash,
    string? ChangeNote,
    int? AppliedCount = null);

public sealed record DocumentOutlineHeadingDto(
    int Level,
    string Heading,
    int Occurrence,
    int StartLine,
    int EndLine,
    int CharOffset,
    int CharLength);

public sealed record DocumentMcpHeadDto(
    Guid Id,
    string Title,
    int CurrentRevisionNumber,
    KnowledgeItemStatus Status,
    int ContentLength,
    string ContentHash,
    IReadOnlyList<DocumentOutlineHeadingDto> Outline,
    bool OutlineTruncated = false);

public sealed record DocumentContentRangeQuery(
    string? Heading,
    int? Occurrence,
    int? StartLine,
    int? LineCount,
    int? Offset,
    int? Limit);

public sealed record DocumentContentRangeDto(
    Guid DocumentId,
    int CurrentRevisionNumber,
    string ContentHash,
    string Content,
    int StartLine,
    int EndLine,
    int CharOffset,
    int CharLength,
    bool Truncated);

public sealed record DocumentSearchQuery(
    string Pattern,
    bool IsRegex = false,
    int ContextLines = DocumentMcpLimits.SearchContextLinesDefault);

public sealed record DocumentSearchHitDto(
    int Line,
    string Text,
    IReadOnlyList<string> Before,
    IReadOnlyList<string> After);

public sealed record DocumentSearchResultDto(
    Guid DocumentId,
    int CurrentRevisionNumber,
    string ContentHash,
    int HitCount,
    IReadOnlyList<DocumentSearchHitDto> Hits,
    bool TruncatedHits);

public sealed record DocumentPatchHunk(string OldText, string NewText, bool ReplaceAll = false);

public sealed record ApplyDocumentPatchRequest(
    int ExpectedRevisionNumber,
    IReadOnlyList<DocumentPatchHunk> Patches,
    string? ChangeNote);

public sealed record DocumentRevisionDiffDto(
    Guid DocumentId,
    int FromRevision,
    int ToRevision,
    string UnifiedDiff,
    bool Truncated = false,
    int OldLineCount = 0,
    int NewLineCount = 0);

public sealed record DocumentReviewContextMcpDto(
    DocumentMcpHeadDto Document,
    int RevisionNumber,
    int? PreviousRevisionNumber,
    string UnifiedDiff,
    bool DiffTruncated,
    IReadOnlyList<CommentDto> Comments,
    IReadOnlyList<DocumentReviewDto> Reviews);
