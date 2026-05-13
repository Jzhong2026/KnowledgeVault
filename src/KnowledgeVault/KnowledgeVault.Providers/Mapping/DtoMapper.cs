using KnowledgeVault.Contracts.Auth;
using KnowledgeVault.Contracts.Categories;
using KnowledgeVault.Contracts.KnowledgeItems;
using KnowledgeVault.Contracts.Tags;
using KnowledgeVault.Domain.Entities;

namespace KnowledgeVault.Providers.Mapping;

internal static class DtoMapper
{
    public static UserProfileDto ToProfileDto(this User user)
    {
        return new UserProfileDto(
            user.Id,
            user.UserName,
            user.Email,
            user.CreatedAt,
            user.LastLoginAt);
    }

    public static CategoryDto ToDto(this Category category)
    {
        return new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.Color,
            category.SortOrder,
            category.IsArchived,
            category.CreatedAt,
            category.UpdatedAt);
    }

    public static TagDto ToDto(this Tag tag)
    {
        return new TagDto(
            tag.Id,
            tag.Name,
            tag.Color,
            tag.KnowledgeItemTags.Count,
            tag.CreatedAt,
            tag.UpdatedAt);
    }

    public static KnowledgeItemSummaryDto ToSummaryDto(this KnowledgeItem item)
    {
        return new KnowledgeItemSummaryDto(
            item.Id,
            item.Title,
            item.Summary,
            item.Status,
            item.Category?.ToDto(),
            item.KnowledgeItemTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.ToDto())
                .OrderBy(x => x.Name)
                .ToArray(),
            item.CreatedAt,
            item.UpdatedAt);
    }

    public static KnowledgeItemDto ToDto(this KnowledgeItem item)
    {
        return new KnowledgeItemDto(
            item.Id,
            item.Title,
            item.Content,
            item.Summary,
            item.SourceUrl,
            item.Status,
            item.Category?.ToDto(),
            item.KnowledgeItemTags
                .Where(x => x.Tag is not null)
                .Select(x => x.Tag!.ToDto())
                .OrderBy(x => x.Name)
                .ToArray(),
            item.CreatedAt,
            item.UpdatedAt,
            item.PublishedAt,
            item.ArchivedAt);
    }
}
