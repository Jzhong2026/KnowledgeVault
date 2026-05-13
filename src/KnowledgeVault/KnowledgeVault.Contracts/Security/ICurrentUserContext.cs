namespace KnowledgeVault.Contracts.Security;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
}
