using KnowledgeVault.Contracts.ApiKeys;
using KnowledgeVault.Contracts.Auth;
using KnowledgeVault.Contracts.Categories;
using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Contracts.Tags;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Providers.Mapping;

internal static class DtoMapper
{
    public static UserProfileDto ToProfileDto(this User user)
    {
        return new UserProfileDto(
            user.Id,
            user.UserName,
            user.Email,
            user.Nickname,
            user.CreatedAt,
            user.LastLoginAt);
    }

    public static string GetDisplayName(User? user)
    {
        return (user is not null && !string.IsNullOrWhiteSpace(user.Nickname) ? user.Nickname : user?.UserName) ?? string.Empty;
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
            category.IsSystem,
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
        var rev = item.CurrentRevision;
        return new KnowledgeItemSummaryDto(
            item.Id,
            item.Scope,
            item.TopicId,
            item.ProjectId,
            item.Project?.Name,
            item.Topic?.Name,
            item.OwnerUserId,
            GetDisplayName(item.OwnerUser),
            item.DocumentType,
            item.CurrentRevisionNumber,
            rev?.Title ?? string.Empty,
            rev?.Summary,
            rev?.LinkDisplayText,
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
        var rev = item.CurrentRevision;
        return new KnowledgeItemDto(
            item.Id,
            item.Scope,
            item.TopicId,
            item.ProjectId,
            item.DocumentType,
            item.CurrentRevisionNumber,
            rev?.Title ?? string.Empty,
            rev?.Content ?? string.Empty,
            rev?.Summary,
            rev?.SourceUrl,
            rev?.LinkDisplayText,
            rev?.LinkUrl,
            rev?.ChangeNote,
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

    public static RevisionSummaryDto ToSummaryDto(this KnowledgeItemRevision revision)
    {
        return new RevisionSummaryDto(
            revision.Id,
            revision.RevisionNumber,
            revision.Title,
            revision.Summary,
            revision.ChangeNote,
            revision.LinkDisplayText,
            revision.CreatedByUserId,
            GetDisplayName(revision.CreatedByUser),
            revision.CreatedAt);
    }

    public static RevisionDto ToDto(this KnowledgeItemRevision revision)
    {
        return new RevisionDto(
            revision.Id,
            revision.RevisionNumber,
            revision.Title,
            revision.Summary,
            revision.Content,
            revision.SourceUrl,
            revision.LinkDisplayText,
            revision.LinkUrl,
            revision.ChangeNote,
            revision.CreatedByUserId,
            GetDisplayName(revision.CreatedByUser),
            revision.CreatedAt);
    }

    public static CommentDto ToDto(this KnowledgeItemComment comment)
    {
        return new CommentDto(
            comment.Id,
            comment.Revision?.RevisionNumber ?? 0,
            comment.ParentCommentId,
            comment.AuthorUserId,
            GetDisplayName(comment.AuthorUser),
            comment.DeletedAt is not null ? string.Empty : comment.Content,
            comment.CreatedAt,
            comment.UpdatedAt,
            comment.DeletedAt is not null,
            comment.ResolvedAt is not null,
            comment.ResolvedByUserId,
            comment.ResolvedByUserId.HasValue ? GetDisplayName(comment.ResolvedByUser) : null,
            comment.ResolvedAt);
    }

    public static DocumentReviewDto ToDto(this DocumentRevisionReview review)
    {
        var revision = review.Revision
            ?? throw new InvalidOperationException("Review revision must be loaded before mapping.");

        return new DocumentReviewDto(
            review.Id,
            revision.KnowledgeItemId,
            revision.Id,
            revision.RevisionNumber,
            revision.KnowledgeItem?.CurrentRevisionNumber == revision.RevisionNumber,
            review.Status,
            review.RequestedByUserId,
            GetDisplayName(review.RequestedByUser),
            review.ReviewerUserId,
            GetDisplayName(review.ReviewerUser),
            review.RequestMessage,
            review.DecisionComment,
            review.CreatedAt,
            review.ReviewedAt);
    }

    public static ProjectDto ToDto(this Project project, ProjectRole? currentUserRole)
    {
        return new ProjectDto(
            project.Id,
            project.Name,
            project.Description,
            project.OwnerUserId,
            project.IsArchived,
            currentUserRole,
            currentUserRole.HasValue,
            project.Members
                .Where(x => x.User is not null)
                .OrderBy(x => x.Role)
                .ThenBy(x => x.User!.UserName)
                .Select(x => x.ToDto())
                .ToArray(),
            project.CreatedAt,
            project.UpdatedAt);
    }

    public static ProjectMemberDto ToDto(this ProjectMember member)
    {
        return new ProjectMemberDto(
            member.UserId,
            member.User?.UserName ?? string.Empty,
            member.User?.Email ?? string.Empty,
            member.Role,
            member.CreatedAt);
    }

    public static ProjectTopicDto ToDto(this ProjectTopic topic)
    {
        return new ProjectTopicDto(
            topic.Id,
            topic.ProjectId,
            topic.Name,
            topic.Description,
            topic.SortOrder,
            topic.IsArchived,
            topic.CreatedAt,
            topic.UpdatedAt);
    }

    public static ApiKeyDto ToDto(this ApiKey apiKey)
    {
        var scopes = string.IsNullOrWhiteSpace(apiKey.Scopes)
            ? Array.Empty<string>()
            : apiKey.Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ApiKeyDto(
            apiKey.Id,
            apiKey.Name,
            apiKey.Prefix,
            scopes,
            apiKey.CreatedAt,
            apiKey.ExpiresAt,
            apiKey.LastUsedAt,
            apiKey.RevokedAt is not null);
    }
}
