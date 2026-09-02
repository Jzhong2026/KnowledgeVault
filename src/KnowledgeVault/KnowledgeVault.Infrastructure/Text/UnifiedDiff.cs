using System.Text;

namespace KnowledgeVault.Infrastructure.Text;

public sealed record UnifiedDiffResult(string Text, bool Truncated, int OldLineCount, int NewLineCount);

/// <summary>
/// Line-oriented unified diff using Hirschberg LCS so large Markdown bodies
/// stay in linear extra memory. Callers must still cap output size: LCS time
/// is O(oldLines × newLines).
/// </summary>
public static class UnifiedDiff
{
    public const int DefaultMaxChars = 16_000;
    public const long DefaultMaxLcsCells = 1_200_000;

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
        if (oldLines.Count == 1 && oldLines[0].Length == 0 && newLines.Count == 1 && newLines[0].Length == 0
            && oldText.Length == 0 && newText.Length == 0)
        {
            return new UnifiedDiffResult($"--- {oldLabel}\n+++ {newLabel}\n", false, 0, 0);
        }

        if ((long)oldLines.Count * newLines.Count > maxLcsCells)
        {
            return new UnifiedDiffResult(
                $"--- {oldLabel}\n+++ {newLabel}\n[diff skipped: {oldLines.Count} vs {newLines.Count} lines exceeds complexity limit; use get_document_content_range]\n",
                Truncated: true,
                oldLines.Count,
                newLines.Count);
        }

        var pairs = FindLcsPairs(oldLines, newLines);
        var rows = BuildRows(oldLines, newLines, pairs);
        var text = Format(rows, oldLines, newLines, oldLabel, newLabel, contextLines);
        if (text.Length > maxChars)
        {
            return new UnifiedDiffResult(
                text[..maxChars] + $"\n[truncated: diff exceeded {maxChars} characters; use get_document_content_range]\n",
                Truncated: true,
                oldLines.Count,
                newLines.Count);
        }

        return new UnifiedDiffResult(text, false, oldLines.Count, newLines.Count);
    }

    private static IReadOnlyList<string> Split(string text)
    {
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        return text.Length == 0 ? [string.Empty] : text.Split('\n');
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

    private static string Format(
        List<(RowKind Kind, int OldIndex, int NewIndex)> rows,
        IReadOnlyList<string> oldLines,
        IReadOnlyList<string> newLines,
        string oldLabel,
        string newLabel,
        int contextLines)
    {
        var builder = new StringBuilder();
        builder.Append("--- ").AppendLine(oldLabel);
        builder.Append("+++ ").AppendLine(newLabel);
        if (rows.All(r => r.Kind == RowKind.Equal))
        {
            return builder.ToString();
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
                var consumed = rows.Take(hunkStart).Count(r => r.Kind != RowKind.Insert);
                oldStart = oldCount == 0 ? consumed : consumed + 1;
            }

            if (firstNew)
            {
                var consumed = rows.Take(hunkStart).Count(r => r.Kind != RowKind.Delete);
                newStart = newCount == 0 ? consumed : consumed + 1;
            }

            builder.Append("@@ -")
                .Append(oldStart)
                .Append(',')
                .Append(oldCount)
                .Append(" +")
                .Append(newStart)
                .Append(',')
                .Append(newCount)
                .AppendLine(" @@");

            for (var i = hunkStart; i <= hunkEnd; i++)
            {
                var row = rows[i];
                switch (row.Kind)
                {
                    case RowKind.Equal:
                        builder.Append(' ').AppendLine(oldLines[row.OldIndex]);
                        break;
                    case RowKind.Delete:
                        builder.Append('-').AppendLine(oldLines[row.OldIndex]);
                        break;
                    case RowKind.Insert:
                        builder.Append('+').AppendLine(newLines[row.NewIndex]);
                        break;
                }
            }

            index = hunkEnd + 1;
        }

        return builder.ToString();
    }
}
