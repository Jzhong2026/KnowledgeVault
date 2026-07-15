using KnowledgeVault.Contracts.Categories;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class CategoryProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : ICategoryProvider
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var query = dbContext.Categories.AsNoTracking().Where(x => x.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var category = await dbContext.Categories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        return category.ToDto();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var name = RequireName(request.Name, 128);
        await EnsureNameAvailableAsync(userId, name, null, cancellationToken);

        var category = new Category
        {
            UserId = userId,
            Name = name,
            NormalizedName = TextNormalizer.NormalizeName(name),
            Description = CleanOptional(request.Description, 512),
            Color = CleanOptional(request.Color, 32),
            SortOrder = request.SortOrder,
            CreatedAt = dateTimeProvider.UtcNow
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        var name = RequireName(request.Name, 128);
        await EnsureNameAvailableAsync(userId, name, id, cancellationToken);

        category.Name = name;
        category.NormalizedName = TextNormalizer.NormalizeName(name);
        category.Description = CleanOptional(request.Description, 512);
        category.Color = CleanOptional(request.Color, 32);
        category.SortOrder = request.SortOrder;
        category.IsArchived = request.IsArchived;
        category.UpdatedAt = dateTimeProvider.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        var now = dateTimeProvider.UtcNow;
        var knowledgeItems = await dbContext.KnowledgeItems
            .Where(x => x.UserId == userId && x.CategoryId == id)
            .ToListAsync(cancellationToken);

        foreach (var knowledgeItem in knowledgeItems)
        {
            knowledgeItem.CategoryId = null;
            knowledgeItem.UpdatedAt = now;
        }

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(Guid userId, string name, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalized = TextNormalizer.NormalizeName(name);
        var exists = await dbContext.Categories.AnyAsync(
            x => x.UserId == userId && x.NormalizedName == normalized && (!currentId.HasValue || x.Id != currentId.Value),
            cancellationToken);

        if (exists)
        {
            throw new ConflictException("Category name already exists.");
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

    private static string RequireName(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Name is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"Name must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }

    private static string? CleanOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException($"Value must be {maxLength} characters or fewer.");
        }

        return trimmed;
    }
}
