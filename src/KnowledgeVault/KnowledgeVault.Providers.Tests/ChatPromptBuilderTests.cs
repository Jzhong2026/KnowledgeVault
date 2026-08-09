using KnowledgeVault.Contracts.Chat;
using KnowledgeVault.Infrastructure.AI;
using KnowledgeVault.Providers.AI;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class ChatPromptBuilderTests
{
    [Fact]
    public void Empty_retrieval_yields_simple_user_message()
    {
        var messages = ChatPromptBuilder.Build("hi", null, Array.Empty<RetrievalResult>(), IntentKind.GeneralQuestion);
        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("user", messages[1].Role);
        Assert.Contains("(none", messages[1].Content);
    }

    [Fact]
    public void Retrieval_renders_with_bracket_indices()
    {
        var retrieval = new[]
        {
            new RetrievalResult(VectorSourceType.Document, "doc-1", "My Doc", "/d/1", "first body", null, Guid.NewGuid(), 0.0),
            new RetrievalResult(VectorSourceType.Review, "rev-1", "Review r1", "/r/1", "second body", null, null, 0.0)
        };
        var messages = ChatPromptBuilder.Build("what?", null, retrieval, IntentKind.FindPlan);
        var user = messages.Last().Content;
        Assert.Contains("[1]", user);
        Assert.Contains("[2]", user);
        Assert.Contains("My Doc", user);
        Assert.Contains("Review r1", user);
    }

    [Fact]
    public void ExtractCitations_parses_unique_bracket_numbers()
    {
        var retrieval = new[]
        {
            new RetrievalResult(VectorSourceType.Document, "d1", "Doc A", "/d/1", "x", null, null, 0.0),
            new RetrievalResult(VectorSourceType.Review, "r1", "Doc B", "/r/1", "y", null, null, 0.0),
            new RetrievalResult(VectorSourceType.Comment, "c1", "Doc C", "/c/1", "z", null, null, 0.0)
        };
        var answer = "The answer is in [1] and [3]; see also [1] again and [2].";
        var citations = ChatPromptBuilder.ExtractCitations(answer, retrieval);
        Assert.Equal(3, citations.Count);
        Assert.Equal("Doc A", citations[0].Title);
        Assert.Equal("Doc B", citations[1].Title);
        Assert.Equal("Doc C", citations[2].Title);
    }

    [Fact]
    public void History_is_capped_to_last_six_messages()
    {
        var history = Enumerable.Range(0, 10).Select(i => new ChatHistoryMessage("user", $"msg-{i}")).ToArray();
        var messages = ChatPromptBuilder.Build("latest", history, Array.Empty<RetrievalResult>(), IntentKind.GeneralQuestion);
        // 1 system + 6 history + 1 user = 8
        Assert.Equal(8, messages.Count);
        Assert.Equal("msg-4", messages[1].Content);
        Assert.Equal("msg-9", messages[6].Content);
    }
}
