namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItemTag
{
    public Guid KnowledgeItemId { get; set; }

    public Guid TagId { get; set; }

    public KnowledgeItem? KnowledgeItem { get; set; }

    public Tag? Tag { get; set; }
}
