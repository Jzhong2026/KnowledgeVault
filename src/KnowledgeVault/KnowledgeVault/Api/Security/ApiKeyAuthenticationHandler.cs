using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Api.Security;

public sealed class ApiKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    private const string Prefix = "kv_";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            return AuthenticateResult.NoResult();
        }

        var authHeader = authHeaderValues.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!token.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var parts = token.Split('_', 3);
        if (parts.Length != 3 || string.IsNullOrEmpty(parts[1]) || string.IsNullOrEmpty(parts[2]))
        {
            return AuthenticateResult.Fail("Invalid API key format.");
        }

        var keyPrefix = parts[1];
        var secret = parts[2];

        await using var scope = Context.RequestServices.CreateAsyncScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeVaultDbContext>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            var apiKey = await dbContext.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Prefix == keyPrefix, Context.RequestAborted);

            if (apiKey is null)
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }

            var providedHash = ApiKeyProvider.ComputeHash(secret);
            if (!SafeEquals(providedHash, apiKey.SecretHash))
            {
                return AuthenticateResult.Fail("Invalid API key.");
            }

            if (apiKey.RevokedAt is not null)
            {
                return AuthenticateResult.Fail("API key has been revoked.");
            }

            var now = dateTimeProvider.UtcNow;
            if (apiKey.ExpiresAt is not null && apiKey.ExpiresAt < now)
            {
                return AuthenticateResult.Fail("API key has expired.");
            }

            var scopes = string.IsNullOrWhiteSpace(apiKey.Scopes)
                ? Array.Empty<string>()
                : apiKey.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, apiKey.UserId.ToString()),
                new(ClaimTypes.Name, apiKey.Name),
                new("kid", apiKey.Id.ToString())
            };
            claims.AddRange(scopes.Select(s => new Claim("scope", s)));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            try
            {
                apiKey.LastUsedAt = now;
                await dbContext.SaveChangesAsync(Context.RequestAborted);
            }
            catch
            {
                // LastUsedAt is best-effort; never fail authentication because of it.
            }

            return AuthenticateResult.Success(ticket);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "API key authentication failed.");
            return AuthenticateResult.Fail("API key authentication failed.");
        }
    }

    private static bool SafeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
