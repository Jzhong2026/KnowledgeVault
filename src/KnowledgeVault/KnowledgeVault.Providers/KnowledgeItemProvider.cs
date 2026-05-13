using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.KnowledgeItems;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class KnowledgeItemProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : IKnowledgeItemProvider
{
    public async Task<PagedResult<KnowledgeItemSummaryDto>> ListAsync(KnowledgeItemQuery query, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var itemsQuery = BuildListQuery(userId, query);

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KnowledgeItemSummaryDto>(
            items.Select(x => x.ToSummaryDto()).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<KnowledgeItemDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var item = await BuildDetailQuery(userId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Knowledge item was not found.");

        return item.ToDto();
    }

    public async Task<KnowledgeItemDto> CreateAsync(CreateKnowledgeItemRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await EnsureCategoryAsync(userId, request.CategoryId, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var item = new KnowledgeItem
        {
            UserId = userId,
            Title = RequireText(request.Title, "Title", 256),
            Content = RequireText(request.Content, "Content", int.MaxValue),
            Summary = CleanOptional(request.Summary, 1024),
            SourceUrl = CleanOptional(request.SourceUrl, 2048),
            CategoryId = request.CategoryId,
            Status = request.Status,
            CreatedAt = now
        };

        ApplyStatusTimestamps(item, now);
        dbContext.KnowledgeItems.Add(item);
        await SyncTagsAsync(userId, item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(userId, item.Id, cancellationToken);
    }

    public async Task<KnowledgeItemDto> UpdateAsync(Guid id, UpdateKnowledgeItemRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await EnsureCategoryAsync(userId, request.CategoryId, cancellationToken);

        var item = await dbContext.KnowledgeItems
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Knowledge item was not found.");

        var now = dateTimeProvider.UtcNow;
        item.Title = RequireText(request.Title, "Title", 256);
        item.Content = RequireText(request.Content, "Content", int.MaxValue);
        item.Summary = CleanOptional(request.Summary, 1024);
        item.SourceUrl = CleanOptional(request.SourceUrl, 2048);
        item.CategoryId = request.CategoryId;
        item.Status = request.Status;
        item.UpdatedAt = now;
        ApplyStatusTimestamps(item, now);

        await SyncTagsAsync(userId, item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(userId, item.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var item = await dbContext.KnowledgeItems.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Knowledge item was not found.");

        var now = dateTimeProvider.UtcNow;
        item.Status = KnowledgeItemStatus.Deleted;
        item.ArchivedAt ??= now;
        item.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<KnowledgeItem> BuildListQuery(Guid userId, KnowledgeItemQuery query)
    {
        var itemsQuery = BuildDetailQuery(userId).Where(x => x.Status != KnowledgeItemStatus.Deleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            itemsQuery = itemsQuery.Where(x =>
                x.Title.Contains(search) ||
                x.Content.Contains(search) ||
                (x.Summary != null && x.Summary.Contains(search)));
        }

        if (query.CategoryId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.Status.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.Status == query.Status.Value);
        }

        var tagIds = query.TagIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        foreach (var tagId in tagIds)
        {
            itemsQuery = itemsQuery.Where(x => x.KnowledgeItemTags.Any(itemTag => itemTag.TagId == tagId));
        }

        return itemsQuery;
    }

    private IQueryable<KnowledgeItem> BuildDetailQuery(Guid userId)
    {
        return dbContext.KnowledgeItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags)
            .ThenInclude(x => x.Tag)
            .Where(x => x.UserId == userId);
    }

    private async Task<KnowledgeItemDto> ReloadAsync(Guid userId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await BuildDetailQuery(userId)
            .FirstAsync(x => x.Id == itemId, cancellationToken);

        return item.ToDto();
    }

    private async Task EnsureCategoryAsync(Guid userId, Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Categories.AnyAsync(
            x => x.Id == categoryId.Value && x.UserId == userId && !x.IsArchived,
            cancellationToken);

        if (!exists)
        {
            throw new ValidationException("Category is invalid or archived.");
        }
    }

    private async Task SyncTagsAsync(
        Guid userId,
        KnowledgeItem item,
        IReadOnlyCollection<Guid>? tagIds,
        IReadOnlyCollection<string>? tagNames,
        CancellationToken cancellationToken)
    {
        var targetTagIds = new HashSet<Guid>((tagIds ?? []).Where(x => x != Guid.Empty));
        var targetTagNames = (tagNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => RequireText(x, "Tag name", 64))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetTagIds.Count > 0)
        {
            var validTagIds = await dbContext.Tags
                .Where(x => x.UserId == userId && targetTagIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (validTagIds.Count != targetTagIds.Count)
            {
                throw new ValidationException("One or more tags are invalid.");
            }
        }

        foreach (var tagName in targetTagNames)
        {
            var normalized = TextNormalizer.NormalizeName(tagName);
            var tag = await dbContext.Tags.FirstOrDefaultAsync(
                x => x.UserId == userId && x.NormalizedName == normalized,
                cancellationToken);

            if (tag is null)
            {
                tag = new Tag
                {
                    UserId = userId,
                    Name = tagName,
                    NormalizedName = normalized,
                    CreatedAt = dateTimeProvider.UtcNow
                };
                dbContext.Tags.Add(tag);
            }

            targetTagIds.Add(tag.Id);
        }

        var existingIds = item.KnowledgeItemTags.Select(x => x.TagId).ToHashSet();
        var toRemove = item.KnowledgeItemTags.Where(x => !targetTagIds.Contains(x.TagId)).ToArray();
        foreach (var link in toRemove)
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

    private static void ApplyStatusTimestamps(KnowledgeItem item, DateTimeOffset now)
    {
        if (item.Status == KnowledgeItemStatus.Active)
        {
            item.PublishedAt ??= now;
            item.ArchivedAt = null;
        }

        if (item.Status is KnowledgeItemStatus.Archived or KnowledgeItemStatus.Deleted)
        {
            item.ArchivedAt ??= now;
        }
    }

    private Guid RequireCurrentUser()
    {
        if (!currentUserContext.IsAuthenticated)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return currentUserContext.UserId;
    }

    private static string RequireText(string value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"{fieldName} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"{fieldName} must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequireText(value, "Value", maxLength);
    }
}
