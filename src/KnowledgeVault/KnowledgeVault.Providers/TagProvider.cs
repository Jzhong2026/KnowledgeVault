using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.Contracts.Tags;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class TagProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : ITagProvider
{
    public async Task<IReadOnlyList<TagDto>> ListAsync(CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var tags = await dbContext.Tags
            .AsNoTracking()
            .Include(x => x.KnowledgeItemTags)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return tags.Select(x => x.ToDto()).ToArray();
    }

    public async Task<TagDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var tag = await dbContext.Tags
            .AsNoTracking()
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Tag was not found.");

        return tag.ToDto();
    }

    public async Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var name = RequireName(request.Name);
        await EnsureNameAvailableAsync(name, null, cancellationToken);

        var tag = new Tag
        {
            Name = name,
            NormalizedName = TextNormalizer.NormalizeName(name),
            Color = CleanOptional(request.Color, 32),
            CreatedAt = dateTimeProvider.UtcNow
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }

    public async Task<TagDto> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var tag = await dbContext.Tags
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Tag was not found.");
        var name = RequireName(request.Name);
        await EnsureNameAvailableAsync(name, id, cancellationToken);

        tag.Name = name;
        tag.NormalizedName = TextNormalizer.NormalizeName(name);
        tag.Color = CleanOptional(request.Color, 32);
        tag.UpdatedAt = dateTimeProvider.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return tag.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureAuthenticated();
        var tag = await dbContext.Tags
            .Include(x => x.KnowledgeItemTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NotFoundException("Tag was not found.");

        dbContext.KnowledgeItemTags.RemoveRange(tag.KnowledgeItemTags);
        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameAvailableAsync(string name, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalized = TextNormalizer.NormalizeName(name);
        var exists = await dbContext.Tags.AnyAsync(
            x => x.NormalizedName == normalized && (!currentId.HasValue || x.Id != currentId.Value),
            cancellationToken);

        if (exists)
        {
            throw new ConflictException("Tag name already exists.");
        }
    }

    private void EnsureAuthenticated()
    {
        if (!currentUserContext.IsAuthenticated || currentUserContext.UserId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Authentication is required.");
        }
    }

    private static string RequireName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Name is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > 64)
        {
            throw new ValidationException("Name must be 64 characters or fewer.");
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
