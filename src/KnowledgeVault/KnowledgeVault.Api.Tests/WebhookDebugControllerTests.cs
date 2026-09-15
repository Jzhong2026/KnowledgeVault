using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeVault.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnowledgeVault.Api.Tests;

// Guards the anonymous webhook capture endpoint used to inspect third-party
// (ElevenLabs) post-call payloads: it must never require auth, must not throw on a
// malformed/empty body, and must mirror the exact bytes to disk.
public sealed class WebhookDebugControllerTests : IDisposable
{
    private readonly string _contentRoot =
        Path.Combine(Path.GetTempPath(), "kv-webhook-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private WebhookDebugController CreateController(string method, string path, string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return new WebhookDebugController(
            NullLogger<WebhookDebugController>.Instance,
            new StubHostEnvironment(_contentRoot))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Fact]
    public async Task Capture_returns_ok_and_mirrors_payload_to_disk()
    {
        const string payload = """{"type":"post_call_transcription","data":{"conversation_id":"conv_abc"}}""";
        var controller = CreateController("POST", "/api/webhook", payload);

        var result = await controller.Capture(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var file = Assert.Single(Directory.GetFiles(Path.Combine(_contentRoot, "logs", "webhooks")));
        var written = await File.ReadAllTextAsync(file);
        Assert.Contains(payload, written);
        Assert.Contains("POST /api/webhook", written);
    }

    [Fact]
    public async Task Capture_tolerates_empty_body()
    {
        var controller = CreateController("POST", "/api/webhook/elevenlabs", string.Empty);

        var result = await controller.Capture(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "KnowledgeVault.Api.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
