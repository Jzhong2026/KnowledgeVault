using KnowledgeVault.Infrastructure.Text;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class MarkdownChunkerTests
{
    [Fact]
    public void Empty_input_returns_no_chunks()
    {
        var chunker = new MarkdownChunker();
        Assert.Empty(chunker.Chunk(""));
        Assert.Empty(chunker.Chunk("   "));
    }

    [Fact]
    public void Single_section_under_max_chars_produces_one_chunk_with_heading_anchor()
    {
        var chunker = new MarkdownChunker { MaxChunkChars = 1000 };
        var chunks = chunker.Chunk("# Hello\n\nWorld body");
        Assert.Single(chunks);
        Assert.Equal("#/hello", chunks[0].Anchor);
        Assert.StartsWith("# Hello", chunks[0].Text);
    }

    [Fact]
    public void Headings_become_separate_anchors()
    {
        var chunker = new MarkdownChunker { MaxChunkChars = 1000 };
        var markdown = """
            # Title

            intro

            ## Section A

            body a

            ## Section B

            body b
            """;
        var chunks = chunker.Chunk(markdown, "/docs/1");
        Assert.Equal(3, chunks.Count);
        Assert.Equal("/docs/1/title", chunks[0].Anchor);
        Assert.Equal("/docs/1/section-a", chunks[1].Anchor);
        Assert.Equal("/docs/1/section-b", chunks[2].Anchor);
    }

    [Fact]
    public void Document_without_headings_uses_root_anchor()
    {
        var chunker = new MarkdownChunker { MaxChunkChars = 1000 };
        var chunks = chunker.Chunk("just some plain text\nwith multiple lines", "/docs/1");
        Assert.Single(chunks);
        Assert.Equal("/docs/1", chunks[0].Anchor);
    }

    [Fact]
    public void Long_section_splits_at_paragraph_boundary_with_overlap()
    {
        var chunker = new MarkdownChunker { MaxChunkChars = 200, OverlapChars = 30 };
        var paragraph = new string('x', 100);
        var markdown = $"""
            # Big

            {paragraph}

            {paragraph}

            {paragraph}
            """;
        var chunks = chunker.Chunk(markdown);
        Assert.True(chunks.Count >= 2, "long section should split into at least two chunks");
        foreach (var c in chunks) Assert.True(c.Text.Length <= 220, $"chunk len {c.Text.Length} exceeded soft cap");
    }
}
