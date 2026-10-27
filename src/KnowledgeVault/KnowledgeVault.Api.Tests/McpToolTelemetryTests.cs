using KnowledgeVault.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Xunit;

namespace KnowledgeVault.Api.Tests;

public sealed class McpToolTelemetryTests
{
    [Fact]
    public void Configurator_registers_a_call_tool_filter()
    {
        using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var options = new McpServerOptions();

        new McpToolTelemetryConfigurator(provider.GetRequiredService<ILoggerFactory>()).Configure(options);

        Assert.Single(options.Filters.Request.CallToolFilters);
    }
}
