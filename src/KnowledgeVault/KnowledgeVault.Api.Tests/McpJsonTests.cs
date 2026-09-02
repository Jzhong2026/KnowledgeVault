using System.Text.Json;
using KnowledgeVault.Api.Mcp;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class McpJsonTests
{
    [Fact]
    public void Serialize_keeps_chinese_and_does_not_pretty_print()
    {
        var payload = new DocumentWriteAckDto(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            2,
            "Agent 编码协作经验",
            KnowledgeItemStatus.Draft,
            12,
            "abc",
            null);
        var json = McpJson.Serialize(payload);
        Assert.Contains("编码", json);
        Assert.DoesNotContain("\\u7F16", json);
        Assert.DoesNotContain("\n  ", json);
        Assert.DoesNotContain("\"content\"", json);
    }
}
