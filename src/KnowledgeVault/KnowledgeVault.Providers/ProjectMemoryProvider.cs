using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class ProjectMemoryProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : IProjectMemoryProvider
{
    private const string MemoryCategoryNormalizedName = "MEMORY";

    public async Task<KnowledgeItemDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        RequireCurrentUser();

        var memoryId = await EnsureExistsAsync(projectId, cancellationToken);
        var memory = await BuildDetailQuery()
            .FirstAsync(x => x.Id == memoryId, cancellationToken);

        return memory.ToDto();
    }

    public async Task<Guid> EnsureExistsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var existingId = await dbContext.KnowledgeItems
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.DocumentType == DocumentType.ProjectMemory)
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (existingId != Guid.Empty)
        {
            return existingId;
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(x => x.Id == projectId)
            .Select(x => new { x.Id, x.OwnerUserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        return await CreateAsync(project.Id, project.OwnerUserId, cancellationToken);
    }

    public async Task<int> EnsureAllExistAsync(CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project => !dbContext.KnowledgeItems.Any(item =>
                item.ProjectId == project.Id && item.DocumentType == DocumentType.ProjectMemory))
            .Select(project => new { project.Id, project.OwnerUserId })
            .ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            await CreateAsync(project.Id, project.OwnerUserId, cancellationToken);
        }

        return projects.Count;
    }

    private async Task<Guid> CreateAsync(
        Guid projectId,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var memoryCategoryId = await dbContext.Categories
            .Where(x => x.IsSystem && x.NormalizedName == MemoryCategoryNormalizedName)
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (memoryCategoryId == Guid.Empty)
        {
            throw new ConflictException("The system Memory category is missing.");
        }

        var itemId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var item = new KnowledgeItem
        {
            Id = itemId,
            OwnerUserId = ownerUserId,
            Scope = DocumentScope.Project,
            ProjectId = projectId,
            TopicId = null,
            CategoryId = memoryCategoryId,
            DocumentType = DocumentType.ProjectMemory,
            CurrentRevisionNumber = 1,
            Status = KnowledgeItemStatus.Active,
            PublishedAt = now,
            CreatedAt = now
        };

        var revision = new KnowledgeItemRevision
        {
            Id = revisionId,
            KnowledgeItemId = itemId,
            RevisionNumber = 1,
            Title = DocumentTemplates.ProjectMemoryTitle,
            Summary = DocumentTemplates.ProjectMemorySummary,
            Content = DocumentTemplates.ProjectMemory,
            ChangeNote = "Created the shared project memory document.",
            CreatedByUserId = ownerUserId,
            CreatedAt = now
        };

        var ownsTransaction = dbContext.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        dbContext.KnowledgeItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        item.CurrentRevisionId = revisionId;
        dbContext.KnowledgeItemRevisions.Add(revision);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return itemId;
    }

    private IQueryable<KnowledgeItem> BuildDetailQuery()
    {
        return dbContext.KnowledgeItems
            .AsNoTracking()
            .Include(x => x.CurrentRevision)
            .Include(x => x.Category)
            .Include(x => x.KnowledgeItemTags)
            .ThenInclude(x => x.Tag);
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
}
