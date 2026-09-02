using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class MarkdownDocumentNavigatorTests
{
    private const string Sample = """
        # Title

        intro

        ## Alpha

        body a
        repeat-me
        repeat-me

        ## Beta

        body b
        """;

    [Fact]
    public void Outline_assigns_line_and_character_ranges()
    {
        var outline = MarkdownDocumentNavigator.BuildOutline(Sample);
        Assert.Equal(3, outline.Count);
        Assert.Equal("Title", outline[0].Heading);
        Assert.Equal(1, outline[0].StartLine);
        Assert.Equal("Alpha", outline[1].Heading);
        Assert.Equal("Beta", outline[2].Heading);
        Assert.True(outline[1].CharLength > 0);
    }

    [Fact]
    public void Range_by_heading_returns_section()
    {
        var range = MarkdownDocumentNavigator.ReadRange(Sample, "Alpha", 1, null, null, null, null);
        Assert.Contains("## Alpha", range.Content);
        Assert.Contains("body a", range.Content);
        Assert.DoesNotContain("## Beta", range.Content);
        Assert.False(range.Truncated);
    }

    [Fact]
    public void Range_by_lines_is_1_based()
    {
        var range = MarkdownDocumentNavigator.ReadRange(Sample, null, null, 1, 2, null, null);
        Assert.Contains("# Title", range.Content);
        Assert.Equal(1, range.StartLine);
        Assert.Equal(2, range.EndLine);
    }

    [Fact]
    public void Range_rejects_mixed_modes()
    {
        Assert.Throws<ValidationException>(() =>
            MarkdownDocumentNavigator.ReadRange(Sample, "Alpha", 1, 1, 2, null, null));
    }

    [Fact]
    public void Search_returns_context_lines()
    {
        var hits = MarkdownDocumentNavigator.Search(Sample, "body a", isRegex: false, contextLines: 1);
        var hit = Assert.Single(hits);
        Assert.Contains("body a", hit.Text);
        Assert.NotEmpty(hit.Before);
    }

    [Fact]
    public void Patch_is_atomic_and_applies_from_the_end()
    {
        var patched = MarkdownDocumentNavigator.ApplyPatches(Sample,
        [
            new MarkdownPatchHunk("body a", "BODY A"),
            new MarkdownPatchHunk("body b", "BODY B")
        ]);
        Assert.Contains("BODY A", patched);
        Assert.Contains("BODY B", patched);
        Assert.DoesNotContain("body a", patched);
    }

    [Fact]
    public void Patch_replace_all_changes_every_match()
    {
        var patched = MarkdownDocumentNavigator.ApplyPatches(Sample,
        [
            new MarkdownPatchHunk("repeat-me", "once", ReplaceAll: true)
        ]);
        Assert.Equal(0, Count(patched, "repeat-me"));
        Assert.Equal(2, Count(patched, "once"));
    }

    [Fact]
    public void Patch_fails_when_old_text_is_ambiguous()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            MarkdownDocumentNavigator.ApplyPatches(Sample,
            [
                new MarkdownPatchHunk("repeat-me", "once")
            ]));
        Assert.Contains("matched 2 times", ex.Message);
    }

    [Fact]
    public void Patch_fails_when_hunks_overlap()
    {
        Assert.Throws<ValidationException>(() =>
            MarkdownDocumentNavigator.ApplyPatches("abcdef",
            [
                new MarkdownPatchHunk("abc", "X"),
                new MarkdownPatchHunk("cde", "Y")
            ]));
    }

    [Fact]
    public void Range_truncates_at_max_chars()
    {
        var body = "# H\n\n" + new string('x', 100);
        var range = MarkdownDocumentNavigator.ReadRange(body, null, null, null, null, 0, 1000, maxChars: 10);
        Assert.True(range.Truncated);
        Assert.Equal(10, range.CharLength);
        Assert.Equal(10, range.Content.Length);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = text.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            start = index + value.Length;
        }
    }
}

public sealed class UnifiedDiffTests
{
    [Fact]
    public void Diff_includes_changed_lines_and_context()
    {
        var diff = UnifiedDiff.Create("one\ntwo\nthree\n", "one\nTWO\nthree\n", "a", "b", contextLines: 1);
        Assert.Contains("-two", diff);
        Assert.Contains("+TWO", diff);
        Assert.Contains(" one", diff);
    }

    [Fact]
    public void Identical_texts_have_no_hunks()
    {
        var diff = UnifiedDiff.Create("same\n", "same\n", "a", "b");
        Assert.DoesNotContain("@@", diff);
    }
}
