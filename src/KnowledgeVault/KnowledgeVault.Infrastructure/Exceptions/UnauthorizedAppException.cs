namespace KnowledgeVault.Infrastructure.Exceptions;

public sealed class UnauthorizedAppException(string message) : AppException(message);
