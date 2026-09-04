using System.Diagnostics;
using System.Text;
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
        Assert.Equal(outline[^1].EndLine, outline[0].EndLine);
        Assert.Equal("Alpha", outline[1].Heading);
        Assert.True(outline[1].EndLine < outline[0].EndLine);
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
        var hit = Assert.Single(hits.Hits);
        Assert.False(hits.TruncatedHits);
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

    [Fact]
    public void Outline_skips_headings_inside_fenced_code()
    {
        var markdown = """
            # Real

            ```
            # example
            ```

            ## Child
            """;
        var outline = MarkdownDocumentNavigator.BuildOutline(markdown);
        Assert.DoesNotContain(outline, x => x.Heading == "example");
        Assert.Contains(outline, x => x.Heading == "Real");
        Assert.Contains(outline, x => x.Heading == "Child");
        Assert.True(outline[0].EndLine >= outline.Single(x => x.Heading == "Child").StartLine);
    }

    [Fact]
    public void Outline_does_not_close_a_fence_with_info_text()
    {
        var markdown = """
            # Real

            ```
            # example
            ```not-a-close
            # still-fenced
            ```

            ## Child
            """;
        var outline = MarkdownDocumentNavigator.BuildOutline(markdown);
        Assert.DoesNotContain(outline, x => x.Heading == "example");
        Assert.DoesNotContain(outline, x => x.Heading == "still-fenced");
        Assert.Contains(outline, x => x.Heading == "Real");
        Assert.Contains(outline, x => x.Heading == "Child");
    }

    [Fact]
    public void Range_by_h1_includes_nested_sections()
    {
        var range = MarkdownDocumentNavigator.ReadRange(Sample, "Title", 1, null, null, null, null);
        Assert.Contains("## Alpha", range.Content);
        Assert.Contains("## Beta", range.Content);
    }

    [Fact]
    public void Search_clips_a_huge_single_line()
    {
        var marker = "needle-token";
        var body = marker + new string('x', 80_000);
        var result = MarkdownDocumentNavigator.Search(body, marker, isRegex: false, contextLines: 0, excerptChars: 240);
        var hit = Assert.Single(result.Hits);
        Assert.True(hit.Text.Length <= 241);
        Assert.DoesNotContain(new string('x', 1_000), hit.Text);
        Assert.Contains("needle", hit.Text);
    }

    [Fact]
    public void Search_does_not_mark_exact_hit_cap_as_truncated()
    {
        var body = string.Join('\n', Enumerable.Range(1, 20).Select(i => $"line-{i} the"));
        var exact = MarkdownDocumentNavigator.Search(body, "the", isRegex: false, maxHits: 20);
        Assert.Equal(20, exact.Hits.Count);
        Assert.False(exact.TruncatedHits);

        var extra = MarkdownDocumentNavigator.Search(body + "\nline-21 the", "the", isRegex: false, maxHits: 20);
        Assert.Equal(20, extra.Hits.Count);
        Assert.True(extra.TruncatedHits);
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

    [Fact]
    public void Pure_insert_hunk_uses_standard_old_start()
    {
        var diff = UnifiedDiff.Create("a\nb\n", "a\nX\nb\n", "old", "new", contextLines: 0);
        Assert.Contains("@@ -1,0 +2,1 @@", diff);
        Assert.Contains("+X", diff);
    }

    [Fact]
    public void Pure_delete_hunk_uses_standard_new_start()
    {
        var diff = UnifiedDiff.Create("a\nX\nb\n", "a\nb\n", "old", "new", contextLines: 0);
        Assert.Contains("@@ -2,1 +1,0 @@", diff);
        Assert.Contains("-X", diff);
    }

    [Fact]
    public void Large_rewrite_is_skipped_before_lcs()
    {
        var oldText = string.Join('\n', Enumerable.Range(0, 2000).Select(i => $"old-{i}"));
        var newText = string.Join('\n', Enumerable.Range(0, 2000).Select(i => $"new-{i}"));
        var result = UnifiedDiff.CreateResult(oldText, newText, "a", "b");
        Assert.True(result.Truncated);
        Assert.Contains("diff skipped", result.Text);
        Assert.Equal(2000, result.OldLineCount);
        Assert.Equal(2000, result.NewLineCount);
    }

    [Fact]
    public void Oversized_diff_text_is_truncated()
    {
        var oldText = string.Join('\n', Enumerable.Range(0, 80).Select(i => new string('a', 200) + i));
        var newText = string.Join('\n', Enumerable.Range(0, 80).Select(i => new string('b', 200) + i));
        var result = UnifiedDiff.CreateResult(oldText, newText, "a", "b", contextLines: 0, maxChars: 1_000);
        Assert.True(result.Truncated);
        Assert.Contains("truncated", result.Text);
        Assert.True(result.Text.Length < 2_000);
    }

    [Fact]
    public void Huge_single_line_change_truncates_before_copying_the_line()
    {
        var oldText = new string('a', 80_000);
        var newText = new string('b', 80_000);
        var result = UnifiedDiff.CreateResult(oldText, newText, "a", "b", contextLines: 0, maxChars: 800);
        Assert.True(result.Truncated);
        Assert.True(result.Text.Length < 1_200);
        Assert.DoesNotContain(new string('a', 1_000), result.Text);
        Assert.DoesNotContain(new string('b', 1_000), result.Text);
        Assert.Contains("truncated", result.Text);
    }

    [Fact]
    public void Large_document_one_line_edit_is_not_skipped()
    {
        var lines = Enumerable.Range(1, 1600).Select(i => $"line {i}").ToArray();
        var oldText = string.Join('\n', lines) + "\n";
        lines[800] = "changed";
        var newText = string.Join('\n', lines) + "\n";
        var result = UnifiedDiff.CreateResult(oldText, newText, "a", "b");
        Assert.False(result.Truncated);
        Assert.DoesNotContain("diff skipped", result.Text);
        Assert.Contains("-line 801", result.Text);
        Assert.Contains("+changed", result.Text);
        Assert.Equal(1600, result.OldLineCount);
        Assert.Equal(1600, result.NewLineCount);
    }

    [Fact]
    public void Trailing_newline_is_not_a_phantom_line()
    {
        var append = UnifiedDiff.CreateResult("a\nb\n", "a\nb\nc\n", "old", "new", contextLines: 0);
        Assert.Equal(2, append.OldLineCount);
        Assert.Equal(3, append.NewLineCount);
        Assert.Contains("@@ -2,0 +3,1 @@", append.Text);
        Assert.Contains("+c", append.Text);

        var deleteLast = UnifiedDiff.CreateResult("a\nb\nc\n", "a\nb\n", "old", "new", contextLines: 0);
        Assert.Equal(3, deleteLast.OldLineCount);
        Assert.Equal(2, deleteLast.NewLineCount);
        Assert.Contains("@@ -3,1 +2,0 @@", deleteLast.Text);
        Assert.Contains("-c", deleteLast.Text);

        var replaceAll = UnifiedDiff.CreateResult("a\nb\nc\n", "x\ny\nz\n", "old", "new", contextLines: 0);
        Assert.Contains("@@ -1,3 +1,3 @@", replaceAll.Text);

        var emptyToContent = UnifiedDiff.CreateResult("", "a\nb\n", "old", "new", contextLines: 0);
        Assert.Equal(0, emptyToContent.OldLineCount);
        Assert.Equal(2, emptyToContent.NewLineCount);
        Assert.Contains("@@ -0,0 +1,2 @@", emptyToContent.Text);
    }

    [Fact]
    public void Diff_output_uses_lf_only()
    {
        var result = UnifiedDiff.CreateResult("one\ntwo\nthree\n", "one\nTWO\nthree\n", "a/doc.md", "b/doc.md");
        Assert.DoesNotContain('\r', result.Text);
        Assert.Contains("--- a/doc.md\n+++ b/doc.md\n", result.Text);
    }

    [Fact]
    public void Git_apply_accepts_end_of_file_hunks()
    {
        AssertGitApply("a\nb\n", "a\nb\nc\n");
        AssertGitApply("a\nb\nc\n", "a\nb\n");
        AssertGitApply("a\nb\nc\n", "a\nX\nc\n");
        AssertGitApply("", "a\nb\n");
    }

    [Fact]
    public void Missing_final_newline_is_marked_for_git_apply()
    {
        var changedLastLine = UnifiedDiff.CreateResult("a\nb", "a\nB", "old", "new", contextLines: 0);
        Assert.Contains("-b\n\\ No newline at end of file\n", changedLastLine.Text);
        Assert.Contains("+B\n\\ No newline at end of file\n", changedLastLine.Text);

        var addedNewline = UnifiedDiff.CreateResult("a\nb", "a\nb\n", "old", "new", contextLines: 0);
        Assert.Contains("-b\n\\ No newline at end of file\n", addedNewline.Text);
        Assert.Contains("+b\n", addedNewline.Text);
        Assert.DoesNotContain("+b\n\\ No newline at end of file", addedNewline.Text);

        var removedNewline = UnifiedDiff.CreateResult("a\nb\n", "a\nb", "old", "new", contextLines: 0);
        Assert.Contains("-b\n+b\n\\ No newline at end of file\n", removedNewline.Text);
    }

    [Fact]
    public void Git_apply_accepts_files_without_trailing_newline()
    {
        AssertGitApply("a\nb", "a\nB");
        AssertGitApply("a\nb", "a\nb\n");
        AssertGitApply("a\nb\n", "a\nb");
    }

    private static void AssertGitApply(string oldText, string newText)
    {
        var git = ResolveGit();
        if (git is null)
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "kv-diff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "doc.md"), Encoding.UTF8.GetBytes(oldText));
            var patch = UnifiedDiff.Create(oldText, newText, "a/doc.md", "b/doc.md");
            File.WriteAllBytes(Path.Combine(root, "change.diff"), Encoding.UTF8.GetBytes(patch));

            var apply = Process.Start(new ProcessStartInfo
            {
                FileName = git,
                Arguments = "-c core.autocrlf=false -c core.eol=lf apply --whitespace=nowarn change.diff",
                WorkingDirectory = root,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }) ?? throw new InvalidOperationException("git apply failed to start.");
            var stderr = apply.StandardError.ReadToEnd();
            Assert.True(apply.WaitForExit(15_000), "git apply timed out.");
            Assert.True(apply.ExitCode == 0, $"git apply failed ({apply.ExitCode}): {stderr}\nPATCH:\n{patch}");

            var applied = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root, "doc.md")));
            Assert.Equal(newText, applied);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string? ResolveGit()
    {
        var fromPath = FindOnPath("git.exe") ?? FindOnPath("git");
        if (fromPath is not null)
        {
            return fromPath;
        }

        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "cmd", "git.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "cmd", "git.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        return path.Split(Path.PathSeparator)
            .Select(dir => Path.Combine(dir, fileName))
            .FirstOrDefault(File.Exists);
    }
}
