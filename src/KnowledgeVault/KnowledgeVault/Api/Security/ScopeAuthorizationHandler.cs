using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace KnowledgeVault.Api.Security;

/// <summary>
/// Enforces <see cref="ScopeRequirement" /> only for requests authenticated with an API key.
/// API keys carry <c>scope</c> claims; a request succeeds only when the required scope is granted.
/// JWT-authenticated users (identified by the absence of the <c>kid</c> claim) are full owners
/// and always satisfy the requirement.
/// </summary>
public sealed class ScopeAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pendingRequirements = context.PendingRequirements
            .OfType<ScopeRequirement>()
            .ToList();

        if (pendingRequirements.Count == 0)
        {
            return Task.CompletedTask;
        }

        var isApiKey = context.User.HasClaim(c => c.Type == "kid");
        if (!isApiKey)
        {
            foreach (var requirement in pendingRequirements)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }

        var grantedScopes = context.User
            .Claims.Where(c => c.Type == "scope")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var requirement in pendingRequirements)
        {
            if (grantedScopes.Contains(requirement.Scope))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
