using KnowledgeVault.Infrastructure.AI;

namespace KnowledgeVault.Providers.AI;

/// <summary>
/// A single retrieved item ready to be cited and quoted in the LLM prompt.
/// </summary>
public sealed record RetrievalResult(
    VectorSourceType Source,
    string SourceId,
    string Title,
    string Anchor,
    string Text,
    Guid? ProjectId,
    Guid? OwnerUserId,
    double Score);
