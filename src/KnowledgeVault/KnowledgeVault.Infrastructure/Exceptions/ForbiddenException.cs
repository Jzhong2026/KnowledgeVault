namespace KnowledgeVault.Infrastructure.Exceptions;

public sealed class ForbiddenException(string message) : AppException(message);
