namespace KnowledgeVault.Infrastructure.Exceptions;

public sealed class NotFoundException(string message) : AppException(message);
