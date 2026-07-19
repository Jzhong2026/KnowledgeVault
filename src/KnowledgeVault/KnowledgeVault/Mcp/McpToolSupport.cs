using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.Api.Mcp;

public static class McpAuthorizationPolicies
{
    public const string ApiKeyOnly = "mcp:api-key";
}

public sealed class McpRequestAuthorizer(
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task RequireScopeAsync(string scope)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null || !principal.HasClaim(claim => claim.Type == "kid"))
        {
            throw new UnauthorizedAppException("A user-created API key is required for MCP access.");
        }

        var result = await authorizationService.AuthorizeAsync(principal, policyName: scope);
        if (!result.Succeeded)
        {
            throw new ForbiddenException($"The current API key does not have {scope} permission.");
        }
    }
}

public abstract class McpOperation(
    IServiceScopeFactory scopeFactory,
    McpRequestAuthorizer authorizer)
{
    protected Task<T> ExecuteAsync<T>(
        string requiredScope,
        Func<IServiceProvider, Task<T>> operation)
    {
        return ExecuteCoreAsync([requiredScope], operation);
    }

    protected Task<T> ExecuteAsync<T>(
        IReadOnlyCollection<string> requiredScopes,
        Func<IServiceProvider, Task<T>> operation)
    {
        return ExecuteCoreAsync(requiredScopes, operation);
    }

    private async Task<T> ExecuteCoreAsync<T>(
        IReadOnlyCollection<string> requiredScopes,
        Func<IServiceProvider, Task<T>> operation)
    {
        foreach (var scope in requiredScopes)
        {
            await authorizer.RequireScopeAsync(scope);
        }

        await using var serviceScope = scopeFactory.CreateAsyncScope();
        return await operation(serviceScope.ServiceProvider);
    }
}

public static class McpJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public static class McpArguments
{
    public static Guid Guid(string value, string argumentName)
    {
        if (!System.Guid.TryParse(value, out var parsed))
        {
            throw new ValidationException($"{argumentName} must be a valid Guid.");
        }

        return parsed;
    }

    public static Guid? OptionalGuid(string? value, string argumentName)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Guid(value, argumentName);
    }

    public static TEnum Enum<TEnum>(string value, string argumentName)
        where TEnum : struct, Enum
    {
        if (!System.Enum.TryParse<TEnum>(value, true, out var parsed) ||
            !System.Enum.IsDefined(parsed))
        {
            throw new ValidationException($"{argumentName} is invalid.");
        }

        return parsed;
    }

    public static TEnum? OptionalEnum<TEnum>(string? value, string argumentName)
        where TEnum : struct, Enum
    {
        return string.IsNullOrWhiteSpace(value) ? null : Enum<TEnum>(value, argumentName);
    }
}
