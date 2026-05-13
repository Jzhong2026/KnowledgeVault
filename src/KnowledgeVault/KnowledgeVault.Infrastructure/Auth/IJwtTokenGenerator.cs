namespace KnowledgeVault.Infrastructure.Auth;

public interface IJwtTokenGenerator
{
    JwtTokenResult GenerateToken(Guid userId, string userName, string email);
}
