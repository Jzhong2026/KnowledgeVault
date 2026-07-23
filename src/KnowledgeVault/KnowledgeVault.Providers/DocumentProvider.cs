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

/// <summary>
/// Orchestrates document use cases. Cross-cutting concerns live in dedicated
/// collaborators: access rules in <see cref="ProjectAccessService"/> /
/// <see cref="IDocumentAccessService"/>, tag sync in <see cref="DocumentTagService"/>,
/// location/folder/category validation in <see cref="DocumentLocationService"/>,
/// and status/revision invariants on <see cref="KnowledgeItem"/> itself.
/// </summary>
public sealed class DocumentProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IDocumentAccessService documentAccessService,
    ProjectAccessService projectAccess,
    DocumentTagService tagService,
    DocumentLocationService locationService) : IDocumentProvider
{
    public async Task<PagedResult<KnowledgeItemSummaryDto>> ListAsync(DocumentQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
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
        var userId = currentUserContext.RequireUserId();
        var query = projectAccess.FilterAccessibleDocuments(
            dbContext.KnowledgeItems.AsNoTracking(),
            userId,
            DocumentScope.Project,
            projectId);

        return await query
            .Select(x => new DocumentOwnerDto(
                x.OwnerUserId,
                x.OwnerUser != null ? (x.OwnerUser.Nickname ?? x.OwnerUser.UserName) : string.Empty))
            .Distinct()
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectDocumentStatsDto> GetProjectDocumentStatsAsync(
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        var projectDocuments = projectAccess.FilterAccessibleDocuments(
            dbContext.KnowledgeItems.AsNoTracking(),
            userId,
            DocumentScope.Project);

        var documentCount = await projectDocuments.CountAsync(cancellationToken);
        var categoryCount = await projectDocuments
            .Where(item => item.CategoryId.HasValue)
            .Select(item => item.CategoryId)
            .Distinct()
            .CountAsync(cancellationToken);
        var tagCount = await projectDocuments
            .SelectMany(item => item.KnowledgeItemTags)
            .Select(itemTag => itemTag.TagId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new ProjectDocumentStatsDto(documentCount, categoryCount, tagCount);
    }

    public async Task<IReadOnlyList<DocumentActivityDayDto>> ListProjectActivityAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int utcOffsetMinutes,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var userId = currentUserContext.RequireUserId();
        if (from >= to)
        {
            throw new ValidationException("The activity start must be earlier than the end.");
        }

        if (to - from > TimeSpan.FromDays(370))
        {
            throw new ValidationException("The activity range cannot exceed 370 days.");
        }

        if (utcOffsetMinutes is < -840 or > 840)
        {
            throw new ValidationException("The UTC offset must be between -840 and 840 minutes.");
        }

        var activityQuery = dbContext.KnowledgeItemRevisions
            .AsNoTracking()
            .Where(revision =>
                revision.CreatedAt >= from &&
                revision.CreatedAt < to &&
                revision.KnowledgeItem != null &&
                revision.KnowledgeItem.Scope == DocumentScope.Project &&
                revision.KnowledgeItem.Status != KnowledgeItemStatus.Deleted &&
                dbContext.ProjectMembers.Any(member =>
                    member.ProjectId == revision.KnowledgeItem.ProjectId &&
                    member.UserId == userId));

        if (projectId.HasValue)
        {
            activityQuery = activityQuery.Where(revision =>
                revision.KnowledgeItem != null &&
                revision.KnowledgeItem.ProjectId == projectId.Value);
        }

        var timestamps = await activityQuery
            .Select(revision => revision.CreatedAt)
            .ToListAsync(cancellationToken);

        return timestamps
            .GroupBy(timestamp => DateOnly.FromDateTime(
                timestamp.UtcDateTime.AddMinutes(utcOffsetMinutes)))
            .OrderBy(group => group.Key)
            .Select(group => new DocumentActivityDayDto(group.Key, group.Count()))
            .ToArray();
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

        var userId = currentUserContext.RequireUserId();
        var now = dateTimeProvider.UtcNow;

        var location = await locationService.ResolveLocationAsync(
            request.Scope,
            request.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);

        var folder = await locationService.ResolveFolderAsync(
            request.Scope, location.ProjectId, request.FolderId, userId, cancellationToken);

        await locationService.EnsureCategoryAsync(request.CategoryId, cancellationToken);

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
            FolderId = folder?.Id,
            DocumentType = request.DocumentType,
            CategoryId = request.CategoryId,
            CreatedAt = now
        };
        item.ChangeStatus(request.Status, now);

        var revision = BuildRevision(item.Id, 1, request.Title, content, request, userId, now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.KnowledgeItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        item.AdvanceToRevision(revision);
        dbContext.KnowledgeItemRevisions.Add(revision);
        await tagService.SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ReloadAsync(item.Id, cancellationToken);
    }

    public async Task<KnowledgeItemDto> UpdateAsync(Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var userId = currentUserContext.RequireUserId();
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

        if (item.IsProjectMemory)
        {
            throw new ValidationException(
                "MEMORY.md cannot be edited directly. Submit a memory candidate for administrator review.");
        }

        var location = await locationService.ResolveLocationAsync(
            item.Scope,
            request.ProjectId ?? item.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);
        await locationService.EnsureCategoryAsync(request.CategoryId, cancellationToken);

        var revision = BuildRevision(
            item.Id, item.CurrentRevisionNumber + 1, request.Title, request.Content, request, userId, now);

        item.AdvanceToRevision(revision);
        item.ProjectId = location.ProjectId;
        item.TopicId = location.TopicId;
        item.CategoryId = request.CategoryId;
        item.UpdatedAt = now;
        item.ChangeStatus(request.Status, now);

        await tagService.SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);

        dbContext.KnowledgeItemRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(item.Id, cancellationToken);
    }

    public async Task UpdateMetadataAsync(Guid id, UpdateDocumentMetadataRequest request, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var userId = currentUserContext.RequireUserId();
        var now = dateTimeProvider.UtcNow;

        var item = await dbContext.KnowledgeItems
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        EnsureProjectMemoryMetadataIsValid(item, request);

        var location = await locationService.ResolveLocationAsync(
            item.Scope,
            request.ProjectId ?? item.ProjectId,
            request.TopicId,
            userId,
            cancellationToken);

        if (request.FolderId.HasValue)
        {
            var folder = await locationService.ResolveFolderAsync(
                item.Scope, item.ProjectId, request.FolderId, userId, cancellationToken);
            item.FolderId = folder?.Id;
        }

        await locationService.EnsureCategoryAsync(request.CategoryId, cancellationToken);
        item.ProjectId = location.ProjectId;
        item.TopicId = location.TopicId;
        item.CategoryId = request.CategoryId;
        item.UpdatedAt = now;
        item.ChangeStatus(request.Status, now);

        await tagService.SyncTagsAsync(item, request.TagIds, request.TagNames, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var now = dateTimeProvider.UtcNow;
        var item = await dbContext.KnowledgeItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        if (item.IsProjectMemory)
        {
            throw new ValidationException("A project's shared MEMORY.md cannot be deleted.");
        }

        item.SoftDelete(now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MoveDocumentAsync(Guid id, Guid? folderId, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureEditAsync(id, cancellationToken);

        var userId = currentUserContext.RequireUserId();
        var now = dateTimeProvider.UtcNow;

        var item = await dbContext.KnowledgeItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Document was not found.");

        var folder = await locationService.ResolveFolderAsync(
            item.Scope, item.ProjectId, folderId, userId, cancellationToken);
        item.FolderId = folder?.Id;
        item.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureProjectMemoryMetadataIsValid(
        KnowledgeItem item,
        UpdateDocumentMetadataRequest request)
    {
        if (!item.IsProjectMemory)
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

    private static KnowledgeItemRevision BuildRevision(
        Guid itemId,
        int revisionNumber,
        string title,
        string content,
        IDocumentContentRequest request,
        Guid userId,
        DateTimeOffset now)
    {
        return new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = itemId,
            RevisionNumber = revisionNumber,
            Title = RequestText.Require(title, "Title", 256),
            Summary = RequestText.Optional(request.Summary, 1024),
            Content = RequestText.Require(content, "Content", int.MaxValue),
            SourceUrl = RequestText.Optional(request.SourceUrl, 2048),
            LinkDisplayText = RequestText.Optional(request.LinkDisplayText, 256),
            LinkUrl = RequestText.Optional(request.LinkUrl, 2048),
            ChangeNote = RequestText.Optional(request.ChangeNote, 1024),
            CreatedByUserId = userId,
            CreatedAt = now
        };
    }

    private IQueryable<KnowledgeItem> BuildListQuery(Guid userId, DocumentQuery query)
    {
        var itemsQuery = projectAccess.FilterAccessibleDocuments(BuildDetailQuery(), userId, query.Scope);

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
}
