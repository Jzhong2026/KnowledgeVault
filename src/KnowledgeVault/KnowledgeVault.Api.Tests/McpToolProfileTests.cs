using System.Reflection;
using KnowledgeVault.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class McpToolProfileTests
{
    [Fact]
    public void Balanced_profile_hides_only_low_frequency_compatibility_tools()
    {
        var disabled = McpToolProfile.GetDisabledTools(new McpToolOptions());

        Assert.Equal(
            [
                "get_document_outline",
                "list_categories",
                "move_document",
                "reindex_knowledge_vault"
            ],
            disabled.OrderBy(name => name));
    }

    [Fact]
    public void Full_profile_keeps_every_tool()
    {
        var disabled = McpToolProfile.GetDisabledTools(new McpToolOptions
        {
            Profile = McpToolProfile.FullProfile
        });

        Assert.Empty(disabled);
    }

    [Fact]
    public void Unknown_profile_is_rejected()
    {
        var options = new McpToolOptions { Profile = "unknown" };

        Assert.Throws<ArgumentException>(() => McpToolProfile.GetDisabledTools(options));
    }

    [Fact]
    public void Apply_removes_disabled_tools_from_the_server_collection()
    {
        var serverOptions = new McpServerOptions();
        serverOptions.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.Ordinal);
        var tools = serverOptions.ToolCollection!;
        tools.Add(CreateTool(nameof(OutlineTool), "get_document_outline"));
        tools.Add(CreateTool(nameof(HeadTool), "get_knowledge_item"));

        McpToolProfile.Apply(serverOptions, new McpToolOptions());

        Assert.DoesNotContain("get_document_outline", serverOptions.ToolCollection.PrimitiveNames);
        Assert.Contains("get_knowledge_item", serverOptions.ToolCollection.PrimitiveNames);
    }

    [Fact]
    public void Post_configurator_filters_tools_after_mcp_registration()
    {
        var services = new ServiceCollection();
        services.AddOptions<McpToolOptions>();
        services.AddSingleton<IPostConfigureOptions<McpServerOptions>, McpToolCollectionPostConfigurator>();
        services.AddMcpServer()
            .WithTools(new[]
            {
                CreateTool(nameof(OutlineTool), "get_document_outline"),
                CreateTool(nameof(HeadTool), "get_knowledge_item")
            });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        Assert.DoesNotContain("get_document_outline", options.ToolCollection!.PrimitiveNames);
        Assert.Contains("get_knowledge_item", options.ToolCollection.PrimitiveNames);
    }

    [Fact]
    public void Default_profile_exposes_the_balanced_knowledge_vault_tool_set()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAuthorization();
        services.AddScoped<McpRequestAuthorizer>();
        services.AddOptions<McpToolOptions>();
        services.AddSingleton<IPostConfigureOptions<McpServerOptions>, McpToolCollectionPostConfigurator>();
        services.AddMcpServer()
            .WithHttpTransport()
            .WithTools<ProjectMcpTools>()
            .WithTools<CategoryMcpTools>()
            .WithTools<DocumentMcpTools>()
            .WithTools<FolderMcpTools>()
            .WithTools<RevisionMcpTools>()
            .WithTools<CommentMcpTools>()
            .WithTools<DocumentReviewMcpTools>()
            .WithTools<ProjectMemoryMcpTools>()
            .WithTools<ChatMcpTools>();

        using var provider = services.BuildServiceProvider();
        var names = provider.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection!.PrimitiveNames;

        Assert.Equal(34, names.Count);
        Assert.DoesNotContain("get_document_outline", names);
        Assert.DoesNotContain("move_document", names);
        Assert.DoesNotContain("list_categories", names);
        Assert.DoesNotContain("reindex_knowledge_vault", names);
        Assert.Contains("get_knowledge_item", names);
        Assert.Contains("get_document_content_range", names);
        Assert.Contains("apply_document_patch", names);
    }

    private static McpServerTool CreateTool(string methodName, string name)
    {
        var method = typeof(McpToolProfileTests).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return McpServerTool.Create(
            method,
            target: null,
            new McpServerToolCreateOptions { Name = name });
    }

    private static string OutlineTool() => string.Empty;

    private static string HeadTool() => string.Empty;
}
