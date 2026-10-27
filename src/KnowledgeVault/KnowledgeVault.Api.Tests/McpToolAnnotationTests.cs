using System.Reflection;
using KnowledgeVault.Api.Mcp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class McpToolAnnotationTests
{
    private static readonly Type[] ToolTypes =
    [
        typeof(ProjectMcpTools),
        typeof(CategoryMcpTools),
        typeof(DocumentMcpTools),
        typeof(FolderMcpTools),
        typeof(RevisionMcpTools),
        typeof(CommentMcpTools),
        typeof(DocumentReviewMcpTools),
        typeof(ProjectMemoryMcpTools),
        typeof(ChatMcpTools)
    ];

    [Fact]
    public void Read_tools_are_emitted_as_read_only_and_non_destructive()
    {
        var violations = EmittedTools()
            .Where(item => IsReadTool(item.Name))
            .Where(item =>
                item.Annotations?.ReadOnlyHint is not true ||
                item.Annotations?.DestructiveHint is true)
            .Select(item => item.Name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Write_tools_are_not_emitted_as_read_only()
    {
        var violations = EmittedTools()
            .Where(item => !IsReadTool(item.Name))
            .Where(item => item.Annotations?.ReadOnlyHint is true)
            .Select(item => item.Name)
            .ToArray();

        Assert.Empty(violations);
    }

    [Fact]
    public void Destructive_and_idempotent_hints_are_explicit()
    {
        var destructive = EmittedTools()
            .Where(item => item.Annotations?.DestructiveHint is true)
            .Select(item => item.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal([nameof(CommentMcpTools.DeleteRevisionComment), nameof(ChatMcpTools.ReindexKnowledgeVault)], destructive);

        var idempotent = EmittedTools()
            .Where(item => item.Annotations?.IdempotentHint is true)
            .Select(item => item.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(ProjectMemoryMcpTools.AcceptProjectMemoryCandidate),
                nameof(DocumentReviewMcpTools.CancelDocumentReview),
                nameof(ProjectMemoryMcpTools.CancelProjectMemoryCandidate),
                nameof(CommentMcpTools.DeleteRevisionComment),
                nameof(DocumentMcpTools.MoveDocument),
                nameof(ChatMcpTools.ReindexKnowledgeVault),
                nameof(CommentMcpTools.ResolveRevisionComment),
                nameof(DocumentMcpTools.UpdateDocumentMetadata),
                nameof(CommentMcpTools.UpdateRevisionComment)
            ],
            idempotent);
    }

    [Fact]
    public void Chatbot_tool_is_marked_open_world()
    {
        var annotations = EmittedTools()
            .Single(item => item.Name == nameof(ChatMcpTools.AskKnowledgeVault))
            .Annotations;

        Assert.True(annotations?.OpenWorldHint);
        Assert.True(annotations?.ReadOnlyHint);
    }

    [Fact]
    public void Server_instructions_fit_the_context_budget()
    {
        Assert.False(string.IsNullOrWhiteSpace(McpServerInstructions.Text));
        Assert.InRange(McpServerInstructions.Text.Length, 1, 800);
    }

    private static IEnumerable<(string Name, ToolAnnotations? Annotations)> EmittedTools()
    {
        foreach (var type in ToolTypes)
        {
            var target = Activator.CreateInstance(type, new object?[] { null, null });
            var methods = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

            foreach (var method in methods)
            {
                var tool = McpServerTool.Create(method, target, new McpServerToolCreateOptions());
                yield return (method.Name, tool.ProtocolTool.Annotations);
            }
        }
    }

    private static bool IsReadTool(string methodName)
    {
        return methodName.StartsWith("List", StringComparison.Ordinal) ||
            methodName.StartsWith("Get", StringComparison.Ordinal) ||
            methodName.StartsWith("Search", StringComparison.Ordinal) ||
            methodName.StartsWith("Ask", StringComparison.Ordinal);
    }
}
