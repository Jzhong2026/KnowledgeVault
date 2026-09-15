using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KnowledgeVault.Api.Controllers;

/// <summary>
/// Anonymous, side-effect free endpoint whose only job is to capture inbound webhook
/// requests (currently used to inspect the payload ElevenLabs posts to us).
///
/// It intentionally does NOT persist to the database and does NOT require authentication,
/// so it can be pointed at from a third-party dashboard before any contract exists.
/// Every request is logged (Serilog structured log) and mirrored as raw text under
/// <c>&lt;ContentRoot&gt;/logs/webhooks/</c> so the exact bytes can be replayed later.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api/webhook")]
public sealed class WebhookDebugController(
    ILogger<WebhookDebugController> logger,
    IHostEnvironment environment) : ControllerBase
{
    // Bodies larger than this are truncated in the log file to avoid unbounded writes.
    private const int MaxCapturedBodyBytes = 256 * 1024;

    [HttpPost]
    [HttpPost("{**path}")]
    public async Task<IActionResult> Capture(CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(cancellationToken);

        logger.LogInformation(
            "Webhook received. Method={Method} Path={Path} Query={Query} ContentType={ContentType} " +
            "RemoteIp={RemoteIp} BodyBytes={BodyBytes} Headers={Headers} Signature={Signature} Body={Body}",
            Request.Method,
            Request.Path.Value,
            Request.QueryString.Value,
            Request.ContentType,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            body.Length,
            DescribeHeaders(),
            Request.Headers["ElevenLabs-Signature"].ToString(),
            body);

        TryAppendToFile(body);

        return Ok(new
        {
            received = true,
            method = Request.Method,
            path = Request.Path.Value,
            bodyBytes = body.Length,
            receivedAtUtc = DateTimeOffset.UtcNow
        });
    }

    // Convenience probe so a browser or the ElevenLabs dashboard can verify the URL is live.
    [HttpGet]
    [HttpGet("{**path}")]
    public IActionResult Ping() => Ok(new { received = true, message = "webhook debug endpoint is live" });

    private async Task<string> ReadBodyAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(
            Request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);

        var body = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        return body;
    }

    private string DescribeHeaders()
    {
        var builder = new StringBuilder();
        foreach (var header in Request.Headers)
        {
            // Never echo bearer tokens that a caller may have attached by mistake.
            var value = string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase)
                ? "***"
                : header.Value.ToString();

            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            builder.Append(header.Key).Append('=').Append(value);
        }

        return builder.ToString();
    }

    private void TryAppendToFile(string body)
    {
        try
        {
            var directory = Path.Combine(environment.ContentRootPath, "logs", "webhooks");
            Directory.CreateDirectory(directory);

            var captured = body.Length > MaxCapturedBodyBytes
                ? body[..MaxCapturedBodyBytes] + "\n...[truncated]"
                : body;

            var entry =
                $"""
                --- {DateTimeOffset.UtcNow:O} {Request.Method} {Request.Path}{Request.QueryString} ---
                {captured}

                """;

            System.IO.File.AppendAllText(
                Path.Combine(directory, $"webhook-{DateTime.UtcNow:yyyyMMdd}.jsonl"),
                entry,
                Encoding.UTF8);
        }
        catch (Exception exception)
        {
            // Debug endpoint: capturing the payload must never make the webhook fail.
            logger.LogWarning(exception, "Failed to mirror webhook payload to disk.");
        }
    }
}
