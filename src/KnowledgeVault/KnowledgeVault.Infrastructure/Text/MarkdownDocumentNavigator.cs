using System.Text;
using System.Text.RegularExpressions;
using KnowledgeVault.Infrastructure.Exceptions;

namespace KnowledgeVault.Infrastructure.Text;

public readonly record struct MarkdownLine(int Number, int CharOffset, int CharLength, string Text);

public sealed record MarkdownHeadingSpan(
    int Level,
    string Heading,
    int Occurrence,
    int StartLine,
    int EndLine,
    int CharOffset,
    int CharLength);

public sealed record MarkdownRangeResult(
    string Content,
    int StartLine,
    int EndLine,
    int CharOffset,
    int CharLength,
    bool Truncated);

public sealed record MarkdownSearchHit(int Line, string Text, IReadOnlyList<string> Before, IReadOnlyList<string> After);

public sealed record MarkdownPatchHunk(string OldText, string NewText, bool ReplaceAll = false);

/// <summary>
/// Heading outline, windowed reads, in-document search, and atomic search-replace
/// for Markdown bodies. Offsets are in the original string (including CR if present).
/// </summary>
public static class MarkdownDocumentNavigator
{
    public const int DefaultRangeMaxChars = 24_000;
    public const int DefaultSearchMaxHits = 20;
    public const int DefaultSearchContextLines = 2;
    public const int MaxSearchContextLines = 8;

    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled);

    public static IReadOnlyList<MarkdownLine> SplitLines(string markdown)
    {
        markdown ??= string.Empty;
        var lines = new List<MarkdownLine>();
        var index = 0;
        var lineNumber = 1;
        while (index < markdown.Length)
        {
            var start = index;
            while (index < markdown.Length && markdown[index] != '\n')
            {
                index++;
            }

            var end = index;
            if (end > start && markdown[end - 1] == '\r')
            {
                end--;
            }

            lines.Add(new MarkdownLine(lineNumber, start, end - start, markdown[start..end]));
            lineNumber++;
            if (index < markdown.Length && markdown[index] == '\n')
            {
                index++;
            }
        }

        if (markdown.Length == 0)
        {
            lines.Add(new MarkdownLine(1, 0, 0, string.Empty));
        }

        return lines;
    }

    public static IReadOnlyList<MarkdownHeadingSpan> BuildOutline(string markdown)
    {
        markdown ??= string.Empty;
        var lines = SplitLines(markdown);
        var headings = new List<(int LineIndex, int Level, string Heading, int CharOffset)>();
        for (var i = 0; i < lines.Count; i++)
        {
            var match = HeadingRegex.Match(lines[i].Text);
            if (!match.Success)
            {
                continue;
            }

            headings.Add((i, match.Groups[1].Value.Length, match.Groups[2].Value.Trim(), lines[i].CharOffset));
        }

        if (headings.Count == 0)
        {
            return
            [
                new MarkdownHeadingSpan(
                    Level: 0,
                    Heading: string.Empty,
                    Occurrence: 1,
                    StartLine: 1,
                    EndLine: lines.Count,
                    CharOffset: 0,
                    CharLength: markdown.Length)
            ];
        }

        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<MarkdownHeadingSpan>(headings.Count);
        for (var i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            occurrences.TryGetValue(heading.Heading, out var seen);
            seen++;
            occurrences[heading.Heading] = seen;

            var startLine = lines[heading.LineIndex].Number;
            var endLineIndex = i + 1 < headings.Count ? headings[i + 1].LineIndex - 1 : lines.Count - 1;
            var endLine = lines[endLineIndex].Number;
            var endChar = lines[endLineIndex].CharOffset + lines[endLineIndex].CharLength;
            result.Add(new MarkdownHeadingSpan(
                heading.Level,
                heading.Heading,
                seen,
                startLine,
                endLine,
                heading.CharOffset,
                Math.Max(0, endChar - heading.CharOffset)));
        }

        return result;
    }

    public static MarkdownRangeResult ReadRange(
        string markdown,
        string? heading,
        int? occurrence,
        int? startLine,
        int? lineCount,
        int? offset,
        int? limit,
        int maxChars = DefaultRangeMaxChars)
    {
        markdown ??= string.Empty;
        maxChars = Math.Max(1, maxChars);
        var modeCount = (string.IsNullOrWhiteSpace(heading) ? 0 : 1)
            + (startLine.HasValue ? 1 : 0)
            + (offset.HasValue ? 1 : 0);
        if (modeCount != 1)
        {
            throw new ValidationException(
                "Provide exactly one range mode: heading, startLine+lineCount, or offset+limit.");
        }

        int start;
        int length;
        int rangeStartLine;
        int rangeEndLine;
        var lines = SplitLines(markdown);

        if (!string.IsNullOrWhiteSpace(heading))
        {
            var wantedOccurrence = occurrence is > 0 ? occurrence.Value : 1;
            var matches = BuildOutline(markdown)
                .Where(x => x.Level > 0 && string.Equals(x.Heading, heading.Trim(), StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new ValidationException($"Heading '{heading.Trim()}' was not found.");
            }

            if (wantedOccurrence > matches.Length)
            {
                throw new ValidationException(
                    $"Heading '{heading.Trim()}' occurs {matches.Length} time(s); occurrence {wantedOccurrence} is out of range.");
            }

            var span = matches[wantedOccurrence - 1];
            start = span.CharOffset;
            length = span.CharLength;
            rangeStartLine = span.StartLine;
            rangeEndLine = span.EndLine;
        }
        else if (startLine.HasValue)
        {
            var count = lineCount ?? throw new ValidationException("lineCount is required when startLine is set.");
            if (startLine.Value < 1)
            {
                throw new ValidationException("startLine must be 1 or greater.");
            }

            if (count < 1)
            {
                throw new ValidationException("lineCount must be 1 or greater.");
            }

            var first = lines.FirstOrDefault(x => x.Number == startLine.Value);
            if (first.Number == 0 && lines.All(x => x.Number != startLine.Value))
            {
                throw new ValidationException($"startLine {startLine.Value} is past the end of the document ({lines.Count} lines).");
            }

            var lastLineNumber = Math.Min(lines.Count, startLine.Value + count - 1);
            var last = lines[lastLineNumber - 1];
            start = first.CharOffset;
            length = last.CharOffset + last.CharLength - start;
            rangeStartLine = startLine.Value;
            rangeEndLine = lastLineNumber;
        }
        else
        {
            start = offset!.Value;
            if (start < 0)
            {
                throw new ValidationException("offset must be 0 or greater.");
            }

            if (start > markdown.Length)
            {
                throw new ValidationException($"offset {start} is past the end of the document ({markdown.Length} characters).");
            }

            var requested = limit ?? maxChars;
            if (requested < 1)
            {
                throw new ValidationException("limit must be 1 or greater.");
            }

            length = Math.Min(requested, markdown.Length - start);
            rangeStartLine = LineAtOffset(lines, start);
            rangeEndLine = LineAtOffset(lines, start + Math.Max(length - 1, 0));
        }

        var truncated = length > maxChars;
        if (truncated)
        {
            length = maxChars;
            rangeEndLine = LineAtOffset(lines, start + Math.Max(length - 1, 0));
        }

        return new MarkdownRangeResult(
            markdown.Substring(start, length),
            rangeStartLine,
            rangeEndLine,
            start,
            length,
            truncated);
    }

    public static IReadOnlyList<MarkdownSearchHit> Search(
        string markdown,
        string pattern,
        bool isRegex,
        int contextLines = DefaultSearchContextLines,
        int maxHits = DefaultSearchMaxHits)
    {
        markdown ??= string.Empty;
        if (string.IsNullOrEmpty(pattern))
        {
            throw new ValidationException("pattern is required.");
        }

        contextLines = Math.Clamp(contextLines, 0, MaxSearchContextLines);
        maxHits = Math.Clamp(maxHits, 1, 100);
        var lines = SplitLines(markdown);
        var hits = new List<MarkdownSearchHit>();
        var seenLines = new HashSet<int>();

        if (isRegex)
        {
            Regex regex;
            try
            {
                regex = new Regex(pattern, RegexOptions.Multiline, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                throw new ValidationException($"Invalid regular expression: {ex.Message}");
            }

            MatchCollection matches;
            try
            {
                matches = regex.Matches(markdown);
            }
            catch (RegexMatchTimeoutException)
            {
                throw new ValidationException("The regular expression timed out.");
            }

            foreach (Match match in matches)
            {
                var line = LineAtOffset(lines, match.Index);
                if (!seenLines.Add(line))
                {
                    continue;
                }

                hits.Add(BuildHit(lines, line, contextLines));
                if (hits.Count >= maxHits)
                {
                    break;
                }
            }
        }
        else
        {
            var start = 0;
            while (start <= markdown.Length)
            {
                var index = markdown.IndexOf(pattern, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                var line = LineAtOffset(lines, index);
                if (seenLines.Add(line))
                {
                    hits.Add(BuildHit(lines, line, contextLines));
                    if (hits.Count >= maxHits)
                    {
                        break;
                    }
                }

                start = index + Math.Max(pattern.Length, 1);
            }
        }

        return hits;
    }

    public static string ApplyPatches(string markdown, IReadOnlyList<MarkdownPatchHunk> hunks)
    {
        markdown ??= string.Empty;
        if (hunks is null || hunks.Count == 0)
        {
            throw new ValidationException("At least one patch hunk is required.");
        }

        var replacements = new List<(int Start, int Length, string NewText, int HunkIndex)>();
        for (var i = 0; i < hunks.Count; i++)
        {
            var hunk = hunks[i];
            if (string.IsNullOrEmpty(hunk.OldText))
            {
                throw new ValidationException($"Patch {i + 1} failed: oldText must not be empty.");
            }

            var matches = FindAll(markdown, hunk.OldText);
            if (matches.Count == 0)
            {
                throw new ValidationException(
                    $"Patch {i + 1} failed: oldText was not found. Nearby excerpt:\n{NearbyExcerpt(markdown, hunk.OldText)}");
            }

            if (matches.Count > 1 && !hunk.ReplaceAll)
            {
                throw new ValidationException(
                    $"Patch {i + 1} failed: oldText matched {matches.Count} times. Add more surrounding text or set replaceAll true.");
            }

            foreach (var matchStart in matches)
            {
                replacements.Add((matchStart, hunk.OldText.Length, hunk.NewText ?? string.Empty, i));
            }
        }

        replacements.Sort((a, b) => a.Start.CompareTo(b.Start));
        for (var i = 1; i < replacements.Count; i++)
        {
            var previous = replacements[i - 1];
            var current = replacements[i];
            if (current.Start < previous.Start + previous.Length)
            {
                throw new ValidationException(
                    $"Patches {previous.HunkIndex + 1} and {current.HunkIndex + 1} overlap. The document was not changed.");
            }
        }

        var builder = new StringBuilder(markdown);
        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var replacement = replacements[i];
            builder.Remove(replacement.Start, replacement.Length);
            builder.Insert(replacement.Start, replacement.NewText);
        }

        return builder.ToString();
    }

    private static List<int> FindAll(string markdown, string oldText)
    {
        var matches = new List<int>();
        var start = 0;
        while (start <= markdown.Length)
        {
            var index = markdown.IndexOf(oldText, start, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            matches.Add(index);
            start = index + Math.Max(oldText.Length, 1);
        }

        return matches;
    }

    private static string NearbyExcerpt(string markdown, string oldText)
    {
        var probeLength = Math.Min(40, oldText.Length);
        var probe = oldText[..probeLength];
        var index = markdown.IndexOf(probe, StringComparison.Ordinal);
        if (index < 0)
        {
            index = 0;
        }

        var start = Math.Max(0, index - 80);
        var length = Math.Min(200, markdown.Length - start);
        return markdown.Substring(start, length);
    }

    private static int LineAtOffset(IReadOnlyList<MarkdownLine> lines, int offset)
    {
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (offset >= lines[i].CharOffset)
            {
                return lines[i].Number;
            }
        }

        return 1;
    }

    private static MarkdownSearchHit BuildHit(IReadOnlyList<MarkdownLine> lines, int lineNumber, int contextLines)
    {
        var line = lines[lineNumber - 1];
        var before = lines
            .Skip(Math.Max(0, lineNumber - 1 - contextLines))
            .Take(Math.Min(contextLines, lineNumber - 1))
            .Select(x => x.Text)
            .ToArray();
        var after = lines
            .Skip(lineNumber)
            .Take(contextLines)
            .Select(x => x.Text)
            .ToArray();
        return new MarkdownSearchHit(lineNumber, line.Text, before, after);
    }
}
