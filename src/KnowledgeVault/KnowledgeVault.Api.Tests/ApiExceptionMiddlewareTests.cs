using System;
using System.Threading.Tasks;
using KnowledgeVault.Api.Middleware;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KnowledgeVault.Api.Tests;

// Guards the exception -> HTTP status mapping at the API boundary.
// ConflictException / ValidationException document the correct current mapping.
// DbUpdateConcurrencyException must map to 409 so a database-level version
// conflict surfaces as a client error (409) instead of 500. This is the
// regression guard for the "并发令牌 + 409 契约" remediation.
public sealed class ApiExceptionMiddlewareTests
{
    private static ApiExceptionMiddleware Middleware(RequestDelegate next) =>
        new(next, NullLogger<ApiExceptionMiddleware>.Instance);

    [Fact]
    public async Task ConflictException_maps_to_409()
    {
        var context = new DefaultHttpContext();
        await Middleware(_ => throw new ConflictException("conflict")).InvokeAsync(context);
        Assert.Equal(409, context.Response.StatusCode);
    }

    [Fact]
    public async Task ValidationException_maps_to_400()
    {
        var context = new DefaultHttpContext();
        await Middleware(_ => throw new ValidationException("bad")).InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_maps_to_409()
    {
        var context = new DefaultHttpContext();
        await Middleware(_ => throw new DbUpdateConcurrencyException("race")).InvokeAsync(context);
        Assert.Equal(409, context.Response.StatusCode);
    }
}
