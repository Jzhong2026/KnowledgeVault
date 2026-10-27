using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace KnowledgeVault.Api.Mcp;

public sealed class McpToolTelemetryConfigurator(ILoggerFactory loggerFactory)
    : IConfigureOptions<McpServerOptions>
{
    public void Configure(McpServerOptions options)
    {
        var logger = loggerFactory.CreateLogger("KnowledgeVault.Mcp.Tools");

        options.Filters.Request.CallToolFilters.Add(next => async (request, cancellationToken) =>
        {
            var started = Stopwatch.GetTimestamp();
            try
            {
                var result = await next(request, cancellationToken);
                WriteLog(logger, request, result, started, exception: null);
                return result;
            }
            catch (Exception exception)
            {
                WriteLog(logger, request, result: null, started, exception);
                throw;
            }
        });
    }

    private static void WriteLog(
        ILogger logger,
        RequestContext<CallToolRequestParams> request,
        CallToolResult? result,
        long started,
        Exception? exception)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var resultChars = result?.Content?
            .OfType<TextContentBlock>()
            .Sum(block => block.Text?.Length ?? 0) ?? 0;

        logger.Log(
            exception is null ? LogLevel.Information : LogLevel.Warning,
            exception,
            "MCP tool {Tool} {Outcome} in {ElapsedMs:0.0} ms with {ArgumentCount} args and {ResultChars} result chars for kid {KeyId}",
            request.Params?.Name ?? "unknown",
            exception is null ? (result?.IsError == true ? "failed" : "succeeded") : "threw",
            elapsedMs,
            request.Params?.Arguments?.Count ?? 0,
            resultChars,
            request.User?.FindFirst("kid")?.Value ?? "unknown");
    }
}
