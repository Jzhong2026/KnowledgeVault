namespace KnowledgeVault.Infrastructure.AI;

/// <summary>
/// Categorizes what kind of entity a vector chunk represents. Used as a
/// permission/filter key and as a citation label in chatbot answers.
/// </summary>
public enum VectorSourceType
{
    Document = 0,
    Revision = 1,
    Review = 2,
    Comment = 3,
    MemoryCandidate = 4,
    MemoryAccepted = 5
}
