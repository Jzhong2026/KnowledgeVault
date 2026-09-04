using System.ComponentModel;
using System.Reflection;
using KnowledgeVault.Api.Mcp;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class McpDocumentToolDescriptionTests
{
    [Fact]
    public void Document_tool_descriptions_stay_within_the_context_budget()
    {
        var descriptions = typeof(DocumentMcpTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => new[]
                    { method.GetCustomAttribute<DescriptionAttribute>()?.Description }
                .Concat(method.GetParameters()
                    .Select(parameter => parameter.GetCustomAttribute<DescriptionAttribute>()?.Description)))
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Select(description => description!)
            .ToArray();

        Assert.NotEmpty(descriptions);
        Assert.True(descriptions.Max(description => description.Length) <= 160);
        Assert.True(descriptions.Sum(description => description.Length) <= 3200);
    }

    [Fact]
    public void Apply_document_patch_replace_all_describes_the_false_default()
    {
        var description = typeof(DocumentMcpTools)
            .GetMethod(nameof(DocumentMcpTools.ApplyDocumentPatch))!
            .GetParameters()
            .Single(parameter => parameter.Name == "replaceAll")
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;

        Assert.Contains("default false", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("by default", description, StringComparison.OrdinalIgnoreCase);
    }
}
