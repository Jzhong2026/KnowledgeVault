using System.ComponentModel.DataAnnotations;

namespace KnowledgeVault.Infrastructure.AI;

public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    [Required]
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    public string ChatModel { get; set; } = "gpt-4o-mini";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public string IntentClassificationModel { get; set; } = "gpt-4o-mini";

    public int ChatTimeoutSeconds { get; set; } = 60;

    public int EmbeddingTimeoutSeconds { get; set; } = 30;
}
