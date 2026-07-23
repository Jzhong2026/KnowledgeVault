using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Domain.Entities;

public sealed class KnowledgeItem : AuditableEntity
{
    public Guid OwnerUserId { get; set; }

    public DocumentScope Scope { get; set; } = DocumentScope.Personal;

    public Guid? ProjectId { get; set; }

    public Guid? TopicId { get; set; }

    public Guid? FolderId { get; set; }

    public Folder? Folder { get; set; }

    public DocumentType DocumentType { get; set; } = DocumentType.General;

    public Guid? CurrentRevisionId { get; set; }

    public int CurrentRevisionNumber { get; set; }

    public Guid? CategoryId { get; set; }

    public KnowledgeItemStatus Status { get; set; } = KnowledgeItemStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public User? OwnerUser { get; set; }

    public Project? Project { get; set; }

    public ProjectTopic? Topic { get; set; }

    public Category? Category { get; set; }

    // Optimistic-concurrency token. SQLite has no native rowversion type, so this
    // is a byte[] concurrency token bumped on every save by RowVersionInterceptor.
    // A write based on a stale snapshot then matches 0 rows and EF raises
    // DbUpdateConcurrencyException (mapped to HTTP 409) instead of silently
    // overwriting the row or failing on a unique index. Nullable so the interceptor
    // can set it before the first insert.
    public byte[]? RowVersion { get; set; }

    public KnowledgeItemRevision? CurrentRevision { get; set; }

    public ICollection<KnowledgeItemRevision> Revisions { get; set; } = [];

    public ICollection<KnowledgeItemTag> KnowledgeItemTags { get; set; } = [];

    /// <summary>True for the auto-managed per-project MEMORY.md document.</summary>
    public bool IsProjectMemory => DocumentType == DocumentType.ProjectMemory;

    /// <summary>
    /// Applies a status change together with its timestamp invariants:
    /// Active sets PublishedAt (once) and clears ArchivedAt; Archived/Deleted
    /// set ArchivedAt (once). This is the only supported way to change Status.
    /// </summary>
    public void ChangeStatus(KnowledgeItemStatus status, DateTimeOffset now)
    {
        Status = status;

        if (Status == KnowledgeItemStatus.Active)
        {
            PublishedAt ??= now;
            ArchivedAt = null;
        }

        if (Status is KnowledgeItemStatus.Archived or KnowledgeItemStatus.Deleted)
        {
            ArchivedAt ??= now;
        }
    }

    /// <summary>
    /// Makes the given revision the current one. The revision must belong to
    /// this document and advance the revision number by exactly one
    /// (or be the initial revision).
    /// </summary>
    public void AdvanceToRevision(KnowledgeItemRevision revision)
    {
        if (revision.KnowledgeItemId != Id)
        {
            throw new InvalidOperationException("The revision does not belong to this document.");
        }

        var expected = CurrentRevisionNumber + 1;
        if (revision.RevisionNumber != expected && !(CurrentRevisionNumber == 0 && revision.RevisionNumber == 1))
        {
            throw new InvalidOperationException(
                $"Revision number {revision.RevisionNumber} does not follow the current revision {CurrentRevisionNumber}.");
        }

        CurrentRevisionId = revision.Id;
        CurrentRevisionNumber = revision.RevisionNumber;
    }

    /// <summary>Soft-deletes the document, preserving the archive timestamp invariant.</summary>
    public void SoftDelete(DateTimeOffset now)
    {
        ChangeStatus(KnowledgeItemStatus.Deleted, now);
        UpdatedAt = now;
    }
}
