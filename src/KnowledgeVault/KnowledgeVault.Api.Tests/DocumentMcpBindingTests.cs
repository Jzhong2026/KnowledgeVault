using KnowledgeVault.Api.Mcp;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class DocumentMcpBindingTests
{
    [Fact]
    public void Bind_patch_hunks_honors_per_hunk_replace_all_flags()
    {
        var hunks = DocumentMcpBinding.BindPatchHunks(
            ["aaa", "bbb"],
            ["A", "B"],
            replaceAll: false,
            replaceAllFlags: [true, false]);

        Assert.Equal(2, hunks.Count);
        Assert.True(hunks[0].ReplaceAll);
        Assert.False(hunks[1].ReplaceAll);
    }

    [Fact]
    public void Bind_patch_hunks_rejects_mismatched_flag_length()
    {
        Assert.Throws<ValidationException>(() =>
            DocumentMcpBinding.BindPatchHunks(["a"], ["b"], false, [true, false]));
    }

    [Fact]
    public void Bind_metadata_omitted_fields_keep_and_empty_string_clears()
    {
        var omitted = DocumentMcpBinding.BindMetadata("Archived", null, null, null, null);
        Assert.True(omitted.Patch);
        Assert.Equal(KnowledgeItemStatus.Archived, omitted.Status);
        Assert.False(omitted.ClearTopic);
        Assert.False(omitted.ClearCategory);
        Assert.False(omitted.UpdateFolder);
        Assert.Null(omitted.TagNames);

        var clear = DocumentMcpBinding.BindMetadata(null, "", "", "", null);
        Assert.True(clear.ClearTopic);
        Assert.True(clear.ClearCategory);
        Assert.True(clear.UpdateFolder);
        Assert.Null(clear.FolderId);
        Assert.Null(clear.Status);
    }

    [Fact]
    public void Metadata_ack_json_does_not_embed_document_body()
    {
        const string marker = "UNIQUE_BODY_MARKER_16K";
        var ack = new DocumentWriteAckDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            "标题",
            KnowledgeItemStatus.Archived,
            20_000,
            "hash",
            null);
        var json = McpJson.Serialize(ack);
        Assert.DoesNotContain(marker, json);
        Assert.DoesNotContain("\"content\"", json);
        Assert.Contains("标题", json);
    }
}
