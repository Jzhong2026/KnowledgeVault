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
    IDocumentAccessService documentAccessService,
    ITicketReferenceParser ticketReferenceParser) : IDocumentProvider
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
            .Where(x => dbContext.ProjectTopics.Any(t =>
                t.Id == x.TopicId &&
                dbContext.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId)));

        if (projectId.HasValue)
        {
            query = query.Where(x => dbContext.ProjectTopics.Any(t =>
                t.Id == x.TopicId && t.ProjectId == projectId.Value));
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
        var userId = RequireCurrentUser();
        var now = dateTimeProvider.UtcNow;

        var ticket = ticketReferenceParser.Parse(request.TicketUrl);

        Guid? topicId = null;
        if (request.Scope == DocumentScope.Project)
        {
            topicId = request.TopicId ?? throw new ValidationException("Project documents must be assigned to a topic.");
            await EnsureCanCreateInTopicAsync(topicId.Value, userId, cancellationToken);
        }
        else if (request.TopicId is not null)
        {
            throw new ValidationException("Personal documents cannot be assigned to a topic.");
        }

        await EnsureCategoryAsync(request.CategoryId, cancellationToken);

        var content = string.IsNullOrWhiteSpace(request.Content)
            ? DocumentTemplates.GetDefaultContent(request.DocumentType) ?? string.Empty
            : request.Content;

        var item = new KnowledgeItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = userId,
            Scope = request.Scope,
            TopicId = topicId,
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
            TicketNo = ticket?.TicketNo,
            TicketUrl = ticket?.TicketUrl,
            ChangeNote = CleanOptional(request.ChangeNote, 1024),
            CreatedByUserId = userId,
            CreatedAt = now
        };

        item.CurrentRevisionId = revision.Id;
        item.CurrentRevisionNumber = 1;

        ApplyStatusTimestamps(item, now);
        dbContext.KnowledgeItems.Add(item);
        dbContext.KnowledgeItemRevisions.Add(revision);
        await SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

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

        var ticket = ticketReferenceParser.Parse(request.TicketUrl);

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
            TicketNo = ticket?.TicketNo,
            TicketUrl = ticket?.TicketUrl,
            ChangeNote = CleanOptional(request.ChangeNote, 1024),
            CreatedByUserId = userId,
            CreatedAt = now
        };

        item.CurrentRevisionId = revision.Id;
        item.CurrentRevisionNumber = nextNumber;
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

        if (item.Scope == DocumentScope.Project)
        {
            var topicId = request.TopicId ?? throw new ValidationException("Project documents must keep a topic.");
            await EnsureTopicBelongsToAccessibleProjectAsync(topicId, userId, cancellationToken);
            item.TopicId = topicId;
        }
        else if (request.TopicId is not null)
        {
            throw new ValidationException("Personal documents cannot be assigned to a topic.");
        }

        await EnsureCategoryAsync(request.CategoryId, cancellationToken);
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

        item.Status = KnowledgeItemStatus.Deleted;
        item.ArchivedAt ??= now;
        item.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
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
                dbContext.ProjectTopics.Any(t => t.Id == x.TopicId && dbContext.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId)));
        }
        else
        {
            itemsQuery = itemsQuery.Where(x =>
                (x.Scope == DocumentScope.Personal && x.OwnerUserId == userId) ||
                (x.Scope == DocumentScope.Project && dbContext.ProjectTopics.Any(t => t.Id == x.TopicId && dbContext.ProjectMembers.Any(m => m.ProjectId == t.ProjectId && m.UserId == userId))));
        }

        if (query.ProjectId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.Scope == DocumentScope.Project &&
                dbContext.ProjectTopics.Any(t => t.Id == x.TopicId && t.ProjectId == query.ProjectId.Value));
        }

        if (query.TopicId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.TopicId == query.TopicId.Value);
        }

        if (query.DocumentType.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.DocumentType == query.DocumentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TicketNo))
        {
            var ticketNo = query.TicketNo.Trim();
            itemsQuery = itemsQuery.Where(x => x.CurrentRevision != null && x.CurrentRevision.TicketNo == ticketNo);
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
            .Include(x => x.Topic).ThenInclude(x => x!.Project)
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

    private async Task EnsureCanCreateInTopicAsync(Guid topicId, Guid userId, CancellationToken cancellationToken)
    {
        var topic = await dbContext.ProjectTopics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topicId && !t.IsArchived, cancellationToken)
            ?? throw new ValidationException("Topic is invalid or archived.");

        var role = await GetProjectRoleAsync(topic.ProjectId, userId, cancellationToken);
        if (role is not (ProjectRole.Owner or ProjectRole.Editor))
        {
            throw new ForbiddenException("You do not have permission to create documents in this topic.");
        }
    }

    private async Task EnsureTopicBelongsToAccessibleProjectAsync(Guid topicId, Guid userId, CancellationToken cancellationToken)
    {
        var projectId = await dbContext.ProjectTopics
            .Where(t => t.Id == topicId && !t.IsArchived)
            .Select(t => t.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (projectId == Guid.Empty)
        {
            throw new ValidationException("Topic is invalid or archived.");
        }

        var role = await GetProjectRoleAsync(projectId, userId, cancellationToken);
        if (role is not (ProjectRole.Owner or ProjectRole.Editor))
        {
            throw new ForbiddenException("You do not have permission to assign this document to the topic.");
        }
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
