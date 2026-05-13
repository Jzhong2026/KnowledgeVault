namespace KnowledgeVault.Infrastructure.Time;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
