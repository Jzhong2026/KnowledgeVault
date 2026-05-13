using KnowledgeVault.Contracts.Auth;

namespace KnowledgeVault.Contracts.Providers;

public interface IAuthProvider
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<UserProfileDto> GetCurrentUserAsync(CancellationToken cancellationToken);
}
