using Microsoft.AspNetCore.Authorization;

namespace KnowledgeVault.Api.Security;

/// <summary>
/// Requirement that the current principal is granted a specific API key scope.
/// JWT-authenticated users are treated as full owners and bypass this requirement
/// (see <see cref="ScopeAuthorizationHandler" />).
/// </summary>
public sealed class ScopeRequirement(string scope) : IAuthorizationRequirement
{
    public string Scope { get; } = scope;
}
