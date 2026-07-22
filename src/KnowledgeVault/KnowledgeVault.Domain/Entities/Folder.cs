using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class Folder : AuditableEntity
{
    public DocumentScope Scope { get; set; } = DocumentScope.Personal;

    // Personal scope: creator/owner. Project scope: null (governed by project membership).
    public Guid? OwnerUserId { get; set; }

    public Guid? ProjectId { get; set; }

    public Guid? ParentFolderId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public Project? Project { get; set; }

    public Folder? ParentFolder { get; set; }

    public ICollection<Folder> ChildFolders { get; set; } = [];

    public ICollection<KnowledgeItem> KnowledgeItems { get; set; } = [];
}
