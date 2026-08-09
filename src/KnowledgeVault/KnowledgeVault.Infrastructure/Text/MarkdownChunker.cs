using System.Text.RegularExpressions;

namespace KnowledgeVault.Infrastructure.Text;

/// <summary>
/// Splits Markdown into retrieval-friendly chunks. Strategy:
///   1) Split the raw text at any line that begins with one or more
///      <c>#</c> characters followed by a space (H1-H6 headings).
///   2) For each slice, if it exceeds <see cref="MaxChunkChars"/>, split it
///      at paragraph boundaries with a <see cref="OverlapChars"/> tail so
///      embeddings can still see context crossing the boundary.
/// Each chunk carries an anchor string (e.g. <c>#/planning-goal</c>) so the
/// chatbot UI can deep-link back to the section.
/// </summary>
public sealed class MarkdownChunker
{
    public int MaxChunkChars { get; init; } = 1500;
    public int OverlapChars { get; init; } = 200;

    // Match the start of a line that begins with 1-6 '#' followed by a space.
    // We deliberately keep the original heading line inside the chunk body so
    // the LLM still sees the surrounding markdown context.
    private static readonly Regex HeadingRegex = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public IReadOnlyList<ChunkSegment> Chunk(string markdown, string documentAnchor = "#")
    {
        if (string.IsNullOrWhiteSpace(markdown)) return Array.Empty<ChunkSegment>();
        var sections = SplitByHeadings(markdown);
        var segments = new List<ChunkSegment>();
        var order = 0;
        foreach (var (headingText, sectionMarkdown) in sections)
        {
            var subChunks = SplitByLength(sectionMarkdown, MaxChunkChars, OverlapChars);
            foreach (var chunk in subChunks)
            {
                var anchor = string.IsNullOrEmpty(headingText)
                    ? documentAnchor
                    : $"{documentAnchor.TrimEnd('/')}/{Slugify(headingText)}";
                segments.Add(new ChunkSegment(chunk, anchor, order++));
            }
        }
        return segments;
    }

    private static List<(string Heading, string Markdown)> SplitByHeadings(string markdown)
    {
        var result = new List<(string, string)>();
        var matches = HeadingRegex.Matches(markdown);
        if (matches.Count == 0)
        {
            // No headings — the whole document is one root section.
            return new List<(string, string)> { (string.Empty, markdown) };
        }

        // Anything before the first heading (a YAML front-matter block, for
        // example) is attached to the root anchor.
        if (matches[0].Index > 0)
        {
            result.Add((string.Empty, markdown[..matches[0].Index]));
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var headingText = m.Groups[2].Value.Trim();
            var startOfBody = m.Index;
            var endOfBody = i + 1 < matches.Count ? matches[i + 1].Index : markdown.Length;
            var sectionBody = markdown.Substring(startOfBody, endOfBody - startOfBody);
            result.Add((headingText, sectionBody));
        }
        return result;
    }

    private static IEnumerable<string> SplitByLength(string text, int max, int overlap)
    {
        if (text.Length <= max) return new[] { text };
        var parts = new List<string>();
        var pos = 0;
        while (pos < text.Length)
        {
            var len = Math.Min(max, text.Length - pos);
            var slice = text.Substring(pos, len);
            // Try to break at last paragraph boundary within the slice.
            if (pos + len < text.Length)
            {
                var lastBreak = slice.LastIndexOf("\n\n", StringComparison.Ordinal);
                if (lastBreak > max / 2)
                {
                    slice = slice[..lastBreak];
                }
            }
            parts.Add(slice);
            if (pos + slice.Length >= text.Length) break;
            pos += Math.Max(1, slice.Length - overlap);
        }
        return parts;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrEmpty(text)) return "-";
        var lower = text.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "-" : slug;
    }
}

public sealed record ChunkSegment(string Text, string Anchor, int Order);
