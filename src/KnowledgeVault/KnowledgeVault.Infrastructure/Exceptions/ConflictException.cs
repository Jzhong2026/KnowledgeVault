namespace KnowledgeVault.Infrastructure.Exceptions;

public sealed class ConflictException(string message) : AppException(message);
