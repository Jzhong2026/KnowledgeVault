using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Documents;
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

public sealed class DocumentProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentAccessService documentAccessService) : IDocumentProvider
{
    public async Task<PagedResult<KnowledgeItemSummaryDto>> ListAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var itemsQuery = BuildListQuery(userId, query);

        var totalCount = await itemsQuery.CountAsync(cancellationToken);
        var items = await itemsQuery
            .ApplyDocumentSort(query.Sort ?? DocumentSort.UpdatedAtDesc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<KnowledgeItemSummaryDto>(
            items.Select(x => x.ToSummaryDto()).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<IReadOnlyList<DocumentOwnerDto>> ListOwnersAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var query = dbContext.KnowledgeItems
            .AsNoTracking()
            .Where(x => x.Scope == DocumentScope.Project && x.Status != KnowledgeItemStatus.Deleted)
            .Where(x => dbContext.ProjectMembers.Any(m =>
                m.ProjectId == x.ProjectId && m.UserId == userId));

        if (projectId.HasValue)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        return await query
            .Select(x => new DocumentOwnerDto(
                x.OwnerUserId,
                x.OwnerUser != null ? (x.OwnerUser.Nickname ?? x.OwnerUser.UserName) : string.Empty))
            .Distinct()
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeItemDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureViewAsync(id, cancellationToken);

        var item = await BuildDetailQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        return item.ToDto();
    }

    public async Task<KnowledgeItemDto> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentType == DocumentType.ProjectMemory)
        {
            throw new ValidationException("MEMORY.md is created and managed automatically for each project.");
        }

        var userId = RequireCurrentUser();
        var now = dateTimeProvider.UtcNow;

        var location = await ResolveLocationAsync(
            request.Scope,
            request.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);

        await EnsureCategoryAsync(request.CategoryId, cancellationToken);

        var content = string.IsNullOrWhiteSpace(request.Content)
            ? DocumentTemplates.GetDefaultContent(request.DocumentType) ?? string.Empty
            : request.Content;

        var item = new KnowledgeItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            Scope = request.Scope,
            ProjectId = location.ProjectId,
            TopicId = location.TopicId,
            DocumentType = request.DocumentType,
            CategoryId = request.CategoryId,
            Status = request.Status,
            CreatedAt = now
        };

        var revision = new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = item.Id,
            RevisionNumber = 1,
            Title = RequireText(request.Title, "Title", 256),
            Summary = CleanOptional(request.Summary, 1024),
            Content = RequireText(content, "Content", int.MaxValue),
            SourceUrl = CleanOptional(request.SourceUrl, 2048),
            LinkDisplayText = CleanOptional(request.LinkDisplayText, 256),
            LinkUrl = CleanOptional(request.LinkUrl, 2048),
            ChangeNote = CleanOptional(request.ChangeNote, 1024),
            CreatedByUserId = userId,
            CreatedAt = now
        };

        ApplyStatusTimestamps(item, now);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.KnowledgeItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        item.CurrentRevisionId = revision.Id;
        item.CurrentRevisionNumber = 1;
        dbContext.KnowledgeItemRevisions.Add(revision);
        await SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ReloadAsync(item.Id, cancellationToken);
    }

    public async Task<KnowledgeItemDto> UpdateAsync(Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var userId = RequireCurrentUser();
        var now = dateTimeProvider.UtcNow;

        var item = await dbContext.KnowledgeItems
            .Include(x => x.Revisions)
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        if (item.CurrentRevisionNumber != request.ExpectedRevisionNumber)
        {
            throw new ConflictException(
                $"The document has been modified. Current revision is {item.CurrentRevisionNumber}.");
        }

        EnsureProjectMemoryUpdateIsValid(item);

        var location = await ResolveLocationAsync(
            item.Scope,
            request.ProjectId ?? item.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);
        await EnsureCategoryAsync(request.CategoryId, cancellationToken);

        var nextNumber = item.CurrentRevisionNumber + 1;
        var revision = new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = item.Id,
            RevisionNumber = nextNumber,
            Title = RequireText(request.Title, "Title", 256),
            Summary = CleanOptional(request.Summary, 1024),
            Content = RequireText(request.Content, "Content", int.MaxValue),
            SourceUrl = CleanOptional(request.SourceUrl, 2048),
            LinkDisplayText = CleanOptional(request.LinkDisplayText, 256),
            LinkUrl = CleanOptional(request.LinkUrl, 2048),
            ChangeNote = CleanOptional(request.ChangeNote, 1024),
            CreatedByUserId = userId,
            CreatedAt = now
        };

        item.CurrentRevisionId = revision.Id;
        item.CurrentRevisionNumber = nextNumber;
        item.ProjectId = location.ProjectId;
        item.TopicId = location.TopicId;
        item.CategoryId = request.CategoryId;
        item.Status = request.Status;
        item.UpdatedAt = now;
        ApplyStatusTimestamps(item, now);

        await SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);

        dbContext.KnowledgeItemRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(item.Id, cancellationToken);
    }

    public async Task UpdateMetadataAsync(Guid id, UpdateDocumentMetadataRequest request, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var userId = RequireCurrentUser();
        var now = dateTimeProvider.UtcNow;

        var item = await dbContext.KnowledgeItems
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        EnsureProjectMemoryMetadataIsValid(item, request);

        var location = await ResolveLocationAsync(
            item.Scope,
            request.ProjectId ?? item.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);

        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
        item.ProjectId = location.ProjectId;
        item.TopicId = location.TopicId;
        item.CategoryId = request.CategoryId;
        item.Status = request.Status;
        item.UpdatedAt = now;
        ApplyStatusTimestamps(item, now);

        await SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var item = await dbContext.KnowledgeItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        if (item.DocumentType == DocumentType.ProjectMemory)
        {
            throw new ValidationException("A project's shared MEMORY.md cannot be deleted.");
        }

        item.Status = KnowledgeItemStatus.Deleted;
        item.ArchivedAt ??= now;
        item.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureProjectMemoryUpdateIsValid(KnowledgeItem item)
    {
        if (item.DocumentType != DocumentType.ProjectMemory)
        {
            return;
        }

        throw new ValidationException(
            "MEMORY.md cannot be edited directly. Submit a memory candidate for administrator review.");
    }

    private static void EnsureProjectMemoryMetadataIsValid(
        KnowledgeItem item,
        UpdateDocumentMetadataRequest request)
    {
        if (item.DocumentType != DocumentType.ProjectMemory)
        {
            return;
        }

        if ((request.ProjectId.HasValue && request.ProjectId != item.ProjectId) ||
            request.TopicId.HasValue ||
            request.CategoryId != item.CategoryId ||
            request.Status != KnowledgeItemStatus.Active)
        {
            throw new ValidationException(
                "MEMORY.md is a fixed active project document; its category, status, and location cannot be changed.");
        }
    }

    private IQueryable<KnowledgeItem> BuildListQuery(Guid userId, DocumentQuery query)
    {
        var itemsQuery = BuildDetailQuery().Where(x => x.Status != KnowledgeItemStatus.Deleted);

        if (query.Scope == DocumentScope.Personal)
        {
            itemsQuery = itemsQuery.Where(x => x.Scope == DocumentScope.Personal && x.OwnerUserId == userId);
        }
        else if (query.Scope == DocumentScope.Project)
        {
            itemsQuery = itemsQuery.Where(x => x.Scope == DocumentScope.Project &&
                dbContext.ProjectMembers.Any(m => m.ProjectId == x.ProjectId && m.UserId == userId));
        }
        else
        {
            itemsQuery = itemsQuery.Where(x =>
                (x.Scope == DocumentScope.Personal && x.OwnerUserId == userId) ||
                (x.Scope == DocumentScope.Project && dbContext.ProjectMembers.Any(m => m.ProjectId == x.ProjectId && m.UserId == userId)));
        }

        if (query.ProjectId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.Scope == DocumentScope.Project &&
                x.ProjectId == query.ProjectId.Value);
        }

        if (query.TopicId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.TopicId == query.TopicId.Value);
        }

        if (query.DocumentType.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.DocumentType == query.DocumentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.LinkDisplayText))
        {
            var linkDisplayText = query.LinkDisplayText.Trim();
            itemsQuery = itemsQuery.Where(x =>
                x.CurrentRevision != null && x.CurrentRevision.LinkDisplayText == linkDisplayText);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            itemsQuery = itemsQuery.Where(x =>
                x.CurrentRevision != null &&
                (x.CurrentRevision.Title.Contains(search) ||
                 (x.CurrentRevision.Summary != null && x.CurrentRevision.Summary.Contains(search)) ||
                 x.CurrentRevision.Content.Contains(search)));
        }

        if (query.CategoryId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.CategoryId == query.CategoryId.Value);
        }

        if (query.OwnerUserId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.OwnerUserId == query.OwnerUserId.Value);
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

    private IQueryable<KnowledgeItem> BuildDetailQuery()
    {
        return dbContext.KnowledgeItems
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Include(x => x.Project)
            .Include(x => x.Topic)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags).ThenInclude(x => x.Tag)
            .Include(x => x.CurrentRevision);
    }

    private async Task<KnowledgeItemDto> ReloadAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await BuildDetailQuery()
            .FirstAsync(x => x.Id == itemId, cancellationToken);

        return item.ToDto();
    }

    private async Task<(Guid? ProjectId, Guid? TopicId)> ResolveLocationAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? topicId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scope == DocumentScope.Personal)
        {
            if (projectId is not null || topicId is not null)
            {
                throw new ValidationException("Personal documents cannot be assigned to a project or group.");
            }

            return (null, null);
        }

        if (projectId is null || projectId == Guid.Empty)
        {
            throw new ValidationException("Project is required for project documents.");
        }

        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId.Value && !x.IsArchived, cancellationToken);
        if (!projectExists)
        {
            throw new ValidationException("Project is invalid or archived.");
        }

        var role = await GetProjectRoleAsync(projectId.Value, userId, cancellationToken);
        if (role is not (ProjectRole.Owner or ProjectRole.Admin or ProjectRole.Editor))
        {
            throw new ForbiddenException("You do not have permission to create or move documents in this project.");
        }

        if (topicId is not null)
        {
            var topicExists = await dbContext.ProjectTopics
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == topicId.Value && x.ProjectId == projectId.Value && !x.IsArchived,
                    cancellationToken);
            if (!topicExists)
            {
                throw new ValidationException("Group is invalid, archived, or belongs to another project.");
            }
        }

        return (projectId, topicId);
    }

    private async Task<ProjectRole?> GetProjectRoleAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        return member?.Role;
    }

    private async Task EnsureCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Categories.AnyAsync(
            x => x.Id == categoryId.Value && !x.IsArchived,
            cancellationToken);

        if (!exists)
        {
            throw new ValidationException("Category is invalid or archived.");
        }
    }

    private async Task SyncTagsAsync(
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
                .Where(x => targetTagIds.Contains(x.Id))
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
                await dbContext.SaveChangesAsync(cancellationToken);
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
        var userId = currentUserContext.UserId;
        if (!currentUserContext.IsAuthenticated || userId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }

        return userId;
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
