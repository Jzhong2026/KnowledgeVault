namespace KnowledgeVault.Providers.AI;

public interface IIntentRouter
{
    Task<IntentKind> ClassifyAsync(
        string message,
        CancellationToken cancellationToken = default);
}
