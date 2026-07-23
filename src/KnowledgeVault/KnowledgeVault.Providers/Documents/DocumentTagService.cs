using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

/// <summary>
/// Owns tag resolution and synchronization for documents.
/// Extracted from DocumentProvider so that tag creation is no longer an
/// inline concern of the document write paths, and no longer performs a
/// mid-operation SaveChanges (tags are persisted by the caller's unit of work).
/// </summary>
public sealed class DocumentTagService(
    KnowledgeVaultDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
{
    /// <summary>
    /// Makes the document's tag links match the requested tag ids + names.
    /// Unknown tag ids are rejected; unknown tag names are created (pending
    /// the caller's SaveChanges).
    /// </summary>
    public async Task SyncTagsAsync(
        KnowledgeItem item,
        IReadOnlyCollection<Guid>? tagIds,
        IReadOnlyCollection<string>? tagNames,
        CancellationToken cancellationToken)
    {
        var targetTagIds = new HashSet<Guid>((tagIds ?? []).Where(x => x != Guid.Empty));
        var targetTagNames = (tagNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => RequestText.Require(x, "Tag name", 64))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        await EnsureTagIdsExistAsync(targetTagIds, cancellationToken);

        // De-duplicate by normalized name within this call so two raw names that
        // normalize identically cannot create duplicate tags in one save.
        var pendingByNormalizedName = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var tagName in targetTagNames)
        {
            var normalized = TextNormalizer.NormalizeName(tagName);
            if (!pendingByNormalizedName.TryGetValue(normalized, out var tag))
            {
                tag = await dbContext.Tags.FirstOrDefaultAsync(
                    x => x.NormalizedName == normalized,
                    cancellationToken);

                if (tag is null)
                {
                    tag = new Tag
                    {
                        Name = tagName,
                        NormalizedName = normalized,
                        CreatedAt = dateTimeProvider.UtcNow
                    };
                    dbContext.Tags.Add(tag);
                }

                pendingByNormalizedName[normalized] = tag;
            }

            targetTagIds.Add(tag.Id);
        }

        var existingIds = item.KnowledgeItemTags.Select(x => x.TagId).ToHashSet();
        foreach (var link in item.KnowledgeItemTags.Where(x => !targetTagIds.Contains(x.TagId)).ToArray())
        {
            item.KnowledgeItemTags.Remove(link);
        }

        foreach (var tagId in targetTagIds.Where(x => !existingIds.Contains(x)))
        {
            item.KnowledgeItemTags.Add(new KnowledgeItemTag
            {
                KnowledgeItemId = item.Id,
                TagId = tagId
            });
        }
    }

    private async Task EnsureTagIdsExistAsync(HashSet<Guid> tagIds, CancellationToken cancellationToken)
    {
        if (tagIds.Count == 0)
        {
            return;
        }

        var validCount = await dbContext.Tags
            .CountAsync(x => tagIds.Contains(x.Id), cancellationToken);

        if (validCount != tagIds.Count)
        {
            throw new ValidationException("One or more tags are invalid.");
        }
    }
}
