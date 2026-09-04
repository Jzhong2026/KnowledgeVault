using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

public sealed class McpToolOptions
{
    public const string SectionName = "Mcp:Tools";

    public string Profile { get; set; } = McpToolProfile.BalancedProfile;
}

public static class McpToolProfile
{
    public const string BalancedProfile = "balanced";
    public const string FullProfile = "full";

    private static readonly IReadOnlySet<string> BalancedDisabledTools = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "get_document_outline",
        "list_categories",
        "move_document",
        "reindex_knowledge_vault"
    };

    public static bool IsKnownProfile(string? profile)
    {
        return string.Equals(profile, BalancedProfile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(profile, FullProfile, StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlySet<string> GetDisabledTools(McpToolOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.Equals(options.Profile, FullProfile, StringComparison.OrdinalIgnoreCase))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        if (string.Equals(options.Profile, BalancedProfile, StringComparison.OrdinalIgnoreCase))
        {
            return BalancedDisabledTools;
        }

        throw new ArgumentException($"Unknown MCP tool profile '{options.Profile}'.", nameof(options));
    }

    public static void Apply(McpServerOptions serverOptions, McpToolOptions toolOptions)
    {
        ArgumentNullException.ThrowIfNull(serverOptions);
        var tools = serverOptions.ToolCollection
            ?? throw new InvalidOperationException("MCP tool collection is not initialized.");

        foreach (var toolName in GetDisabledTools(toolOptions))
        {
            if (tools.TryGetPrimitive(toolName, out var tool) && tool is not null)
            {
                tools.Remove(tool);
            }
        }
    }
}

public sealed class McpToolCollectionPostConfigurator(IOptions<McpToolOptions> toolOptions)
    : IPostConfigureOptions<McpServerOptions>
{
    public void PostConfigure(string? name, McpServerOptions options)
    {
        McpToolProfile.Apply(options, toolOptions.Value);
    }
}
