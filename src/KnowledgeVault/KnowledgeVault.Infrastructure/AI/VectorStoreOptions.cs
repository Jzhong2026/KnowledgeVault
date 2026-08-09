using System.ComponentModel.DataAnnotations;

namespace KnowledgeVault.Infrastructure.AI;

public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";

    public string Provider { get; set; } = "Chroma";

    [Required]
    public string Endpoint { get; set; } = "http://localhost:8000";

    public string Collection { get; set; } = "knowledge_vault_chunks";

    public string Distance { get; set; } = "cosine";

    public string ApiKey { get; set; } = string.Empty;
}
