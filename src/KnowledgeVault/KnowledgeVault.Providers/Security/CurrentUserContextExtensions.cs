using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Infrastructure.Exceptions;

namespace KnowledgeVault.Providers;

/// <summary>
/// Shared authentication guard used by all providers. Replaces the
/// per-provider RequireCurrentUser/EnsureAuthenticated copies.
/// </summary>
public static class CurrentUserContextExtensions
{
    /// <summary>Returns the authenticated user's id or throws 401.</summary>
    public static Guid RequireUserId(this ICurrentUserContext currentUserContext)
    {
        var userId = currentUserContext.UserId;
        if (!currentUserContext.IsAuthenticated || userId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return userId;
    }
}
