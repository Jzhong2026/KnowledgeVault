using System.Text;

namespace KnowledgeVault.Infrastructure.Text;

public sealed record UnifiedDiffResult(string Text, bool Truncated, int OldLineCount, int NewLineCount);

/// <summary>
/// Line-oriented unified diff using Hirschberg LCS so large Markdown bodies
/// stay in linear extra memory. Common prefix/suffix are stripped before the
/// O(oldLines × newLines) LCS, so a one-line edit in a large file stays cheap.
/// Output always uses LF so <c>git apply</c> and JS/Python parsers see Unix diffs.
/// </summary>
public static class UnifiedDiff
{
    public const int DefaultMaxChars = 16_000;
    public const long DefaultMaxLcsCells = 1_200_000;

    /// <summary>
    /// Text-only helper. Prefer <see cref="CreateResult"/> when callers need
    /// <see cref="UnifiedDiffResult.Truncated"/> or exact line counts.
    /// </summary>
    public static string Create(
        string oldText,
        string newText,
        string oldLabel,
        string newLabel,
        int contextLines = 3) =>
        CreateResult(oldText, newText, oldLabel, newLabel, contextLines).Text;

    public static UnifiedDiffResult CreateResult(
        string oldText,
        string newText,
        string oldLabel,
        string newLabel,
        int contextLines = 3,
        int maxChars = DefaultMaxChars,
        long maxLcsCells = DefaultMaxLcsCells)
    {
        oldText ??= string.Empty;
        newText ??= string.Empty;
        contextLines = Math.Max(0, contextLines);
        maxChars = Math.Max(256, maxChars);
        var oldLines = Split(oldText);
        var newLines = Split(newText);
        if (oldLines.Count == 0 && newLines.Count == 0)
        {
            return new UnifiedDiffResult($"--- {oldLabel}\n+++ {newLabel}\n", false, 0, 0);
        }

        var (prefix, suffix) = CommonAffix(oldLines, newLines);
        var oldSpan = oldLines.Count - prefix - suffix;
        var newSpan = newLines.Count - prefix - suffix;
        if (oldSpan == 0 && newSpan == 0)
        {
            return new UnifiedDiffResult(
                $"--- {oldLabel}\n+++ {newLabel}\n",
                false,
                oldLines.Count,
                newLines.Count);
        }

        if ((long)oldSpan * newSpan > maxLcsCells)
        {
            return new UnifiedDiffResult(
                $"--- {oldLabel}\n+++ {newLabel}\n[diff skipped: {oldLines.Count} vs {newLines.Count} lines exceeds complexity limit; use get_document_content_range]\n",
                Truncated: true,
                oldLines.Count,
                newLines.Count);
        }

        var oldSlice = Slice(oldLines, prefix, oldSpan);
        var newSlice = Slice(newLines, prefix, newSpan);
        var pairs = FindLcsPairs(oldSlice, newSlice);
        var middle = BuildRows(oldSlice, newSlice, pairs);
        var contextPrefix = Math.Min(contextLines, prefix);
        var contextSuffix = Math.Min(contextLines, suffix);
        var rows = new List<(RowKind Kind, int OldIndex, int NewIndex)>(
            middle.Count + contextPrefix + contextSuffix);
        for (var i = prefix - contextPrefix; i < prefix; i++)
        {
            rows.Add((RowKind.Equal, i, i));
        }

        foreach (var row in middle)
        {
            rows.Add((
                row.Kind,
                row.OldIndex < 0 ? -1 : row.OldIndex + prefix,
                row.NewIndex < 0 ? -1 : row.NewIndex + prefix));
        }

        for (var i = 0; i < contextSuffix; i++)
        {
            rows.Add((
                RowKind.Equal,
                oldLines.Count - suffix + i,
                newLines.Count - suffix + i));
        }

        var (text, truncated) = Format(
            rows,
            oldLines,
            newLines,
            oldLabel,
            newLabel,
            contextLines,
            prefix - contextPrefix,
            maxChars);
        if (truncated)
        {
            return new UnifiedDiffResult(text, true, oldLines.Count, newLines.Count);
        }

        return new UnifiedDiffResult(text, false, oldLines.Count, newLines.Count);
    }

    private static IReadOnlyList<string> Split(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (text.Length == 0)
        {
            return [];
        }

        var lines = text.Split('\n');
        return text[^1] == '\n'
            ? lines.AsSpan(0, lines.Length - 1).ToArray()
            : lines;
    }

    private static (int Prefix, int Suffix) CommonAffix(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        var prefix = 0;
        var limit = Math.Min(oldLines.Count, newLines.Count);
        while (prefix < limit && oldLines[prefix] == newLines[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < oldLines.Count - prefix &&
               suffix < newLines.Count - prefix &&
               oldLines[oldLines.Count - 1 - suffix] == newLines[newLines.Count - 1 - suffix])
        {
            suffix++;
        }

        return (prefix, suffix);
    }

    private static List<string> Slice(IReadOnlyList<string> lines, int start, int count)
    {
        var slice = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            slice.Add(lines[start + i]);
        }

        return slice;
    }

    private static List<(int OldIndex, int NewIndex)> FindLcsPairs(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines)
    {
        var pairs = new List<(int, int)>();
        CollectLcsPairs(oldLines, 0, oldLines.Count, newLines, 0, newLines.Count, pairs);
        return pairs;
    }

    private static void CollectLcsPairs(
        IReadOnlyList<string> oldLines, int oldStart, int oldEnd,
        IReadOnlyList<string> newLines, int newStart, int newEnd,
        List<(int OldIndex, int NewIndex)> pairs)
    {
        if (oldStart == oldEnd || newStart == newEnd)
        {
            return;
        }

        if (oldEnd - oldStart == 1)
        {
            for (var i = newStart; i < newEnd; i++)
            {
                if (oldLines[oldStart] == newLines[i])
                {
                    pairs.Add((oldStart, i));
                    return;
                }
            }

            return;
        }

        var oldMiddle = (oldStart + oldEnd) / 2;
        var forward = LcsLengths(oldLines, oldStart, oldMiddle, newLines, newStart, newEnd, reverse: false);
        var backward = LcsLengths(oldLines, oldMiddle, oldEnd, newLines, newStart, newEnd, reverse: true);
        var split = 0;
        var bestScore = -1;
        var span = newEnd - newStart;
        for (var i = 0; i <= span; i++)
        {
            var score = forward[i] + backward[span - i];
            if (score > bestScore)
            {
                bestScore = score;
                split = i;
            }
        }

        var newMiddle = newStart + split;
        CollectLcsPairs(oldLines, oldStart, oldMiddle, newLines, newStart, newMiddle, pairs);
        CollectLcsPairs(oldLines, oldMiddle, oldEnd, newLines, newMiddle, newEnd, pairs);
    }

    private static int[] LcsLengths(
        IReadOnlyList<string> oldLines, int oldStart, int oldEnd,
        IReadOnlyList<string> newLines, int newStart, int newEnd,
        bool reverse)
    {
        var length = newEnd - newStart;
        var previous = new int[length + 1];
        for (var offset = 0; offset < oldEnd - oldStart; offset++)
        {
            var oldValue = oldLines[reverse ? oldEnd - offset - 1 : oldStart + offset];
            var current = new int[length + 1];
            for (var newOffset = 0; newOffset < length; newOffset++)
            {
                var newValue = newLines[reverse ? newEnd - newOffset - 1 : newStart + newOffset];
                current[newOffset + 1] = oldValue == newValue
                    ? previous[newOffset] + 1
                    : Math.Max(previous[newOffset + 1], current[newOffset]);
            }

            previous = current;
        }

        return previous;
    }

    private enum RowKind { Equal, Delete, Insert }

    private static List<(RowKind Kind, int OldIndex, int NewIndex)> BuildRows(
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines,
        List<(int OldIndex, int NewIndex)> pairs)
    {
        var rows = new List<(RowKind, int, int)>();
        var oldIndex = 0;
        var newIndex = 0;
        foreach (var (matchOld, matchNew) in pairs)
        {
            while (oldIndex < matchOld)
            {
                rows.Add((RowKind.Delete, oldIndex, -1));
                oldIndex++;
            }

            while (newIndex < matchNew)
            {
                rows.Add((RowKind.Insert, -1, newIndex));
                newIndex++;
            }

            rows.Add((RowKind.Equal, matchOld, matchNew));
            oldIndex = matchOld + 1;
            newIndex = matchNew + 1;
        }

        while (oldIndex < oldLines.Count)
        {
            rows.Add((RowKind.Delete, oldIndex, -1));
            oldIndex++;
        }

        while (newIndex < newLines.Count)
        {
            rows.Add((RowKind.Insert, -1, newIndex));
            newIndex++;
        }

        return rows;
    }

    private static (string Text, bool Truncated) Format(
        List<(RowKind Kind, int OldIndex, int NewIndex)> rows,
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines,
        string oldLabel,
        string newLabel,
        int contextLines,
        int hiddenPrefixLines,
        int maxChars)
    {
        var builder = new StringBuilder();
        AppendLf(builder, "--- ", oldLabel);
        AppendLf(builder, "+++ ", newLabel);
        if (rows.Count == 0 || rows.All(r => r.Kind == RowKind.Equal))
        {
            return (builder.ToString(), false);
        }

        var changeFlags = rows.Select(r => r.Kind != RowKind.Equal).ToArray();
        var include = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            if (!changeFlags[i])
            {
                continue;
            }

            for (var j = Math.Max(0, i - contextLines); j <= Math.Min(rows.Count - 1, i + contextLines); j++)
            {
                include[j] = true;
            }
        }

        var truncated = false;
        var index = 0;
        while (index < rows.Count)
        {
            if (!include[index])
            {
                index++;
                continue;
            }

            var hunkStart = index;
            var hunkEnd = index;
            while (hunkEnd + 1 < rows.Count && include[hunkEnd + 1])
            {
                hunkEnd++;
            }

            var oldStart = 0;
            var newStart = 0;
            var oldCount = 0;
            var newCount = 0;
            var firstOld = true;
            var firstNew = true;
            for (var i = hunkStart; i <= hunkEnd; i++)
            {
                var row = rows[i];
                if (row.Kind is RowKind.Equal or RowKind.Delete)
                {
                    if (firstOld)
                    {
                        oldStart = row.OldIndex + 1;
                        firstOld = false;
                    }

                    oldCount++;
                }

                if (row.Kind is RowKind.Equal or RowKind.Insert)
                {
                    if (firstNew)
                    {
                        newStart = row.NewIndex + 1;
                        firstNew = false;
                    }

                    newCount++;
                }
            }

            if (firstOld)
            {
                var consumed = hiddenPrefixLines + rows.Take(hunkStart).Count(r => r.Kind != RowKind.Insert);
                oldStart = oldCount == 0 ? consumed : consumed + 1;
            }

            if (firstNew)
            {
                var consumed = hiddenPrefixLines + rows.Take(hunkStart).Count(r => r.Kind != RowKind.Delete);
                newStart = newCount == 0 ? consumed : consumed + 1;
            }

            var header = $"@@ -{oldStart},{oldCount} +{newStart},{newCount} @@\n";
            var hunkSize = header.Length;
            for (var i = hunkStart; i <= hunkEnd; i++)
            {
                hunkSize += 1 + LineText(rows[i], oldLines, newLines).Length + 1;
            }

            if (builder.Length + hunkSize > maxChars)
            {
                truncated = true;
                break;
            }

            builder.Append(header);
            for (var i = hunkStart; i <= hunkEnd; i++)
            {
                var row = rows[i];
                switch (row.Kind)
                {
                    case RowKind.Equal:
                        AppendLf(builder, " ", oldLines[row.OldIndex]);
                        break;
                    case RowKind.Delete:
                        AppendLf(builder, "-", oldLines[row.OldIndex]);
                        break;
                    case RowKind.Insert:
                        AppendLf(builder, "+", newLines[row.NewIndex]);
                        break;
                }
            }

            index = hunkEnd + 1;
        }

        if (truncated)
        {
            builder.Append("[truncated: diff exceeded ")
                .Append(maxChars)
                .Append(" characters; use get_document_content_range]\n");
        }

        return (builder.ToString(), truncated);
    }

    private static string LineText(
        (RowKind Kind, int OldIndex, int NewIndex) row,
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines) =>
        row.Kind switch
        {
            RowKind.Insert => newLines[row.NewIndex],
            _ => oldLines[row.OldIndex]
        };

    private static void AppendLf(StringBuilder builder, string prefix, string text)
    {
        builder.Append(prefix).Append(text).Append('\n');
    }
}
