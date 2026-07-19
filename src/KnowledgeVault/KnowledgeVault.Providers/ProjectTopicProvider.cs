using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;
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

public sealed class ProjectTopicProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider) : IProjectTopicProvider
{
    public async Task<PagedResult<ProjectTopicDto>> ListAsync(Guid projectId, ProjectTopicQuery query, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireMembershipAsync(projectId, userId, cancellationToken);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = dbContext.ProjectTopics
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .Where(t => query.IncludeArchived || !t.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(t =>
                t.Name.Contains(search) ||
                (t.Description != null && t.Description.Contains(search)));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var topics = await baseQuery
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = topics.Select(t => t.ToDto()).ToArray();
        return new PagedResult<ProjectTopicDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProjectTopicDto> GetAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireMembershipAsync(projectId, userId, cancellationToken);

        var topic = await dbContext.ProjectTopics
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == topicId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Project group was not found.");

        return topic.ToDto();
    }

    public async Task<ProjectTopicDto> CreateAsync(Guid projectId, CreateProjectTopicRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireRoleAtLeastAsync(projectId, userId, ProjectRole.Editor, cancellationToken);

        var name = RequireText(request.Name, "Name", 128);
        var normalizedName = TextNormalizer.NormalizeName(name);

        if (await dbContext.ProjectTopics.AnyAsync(
                t => t.ProjectId == projectId && t.NormalizedName == normalizedName, cancellationToken))
        {
            throw new ValidationException("A group with this name already exists in the project.");
        }

        var topic = new ProjectTopic
        {
            ProjectId = projectId,
            Name = name,
            NormalizedName = normalizedName,
            Description = CleanOptional(request.Description, 512),
            SortOrder = request.SortOrder,
            IsArchived = false,
            CreatedAt = dateTimeProvider.UtcNow
        };

        dbContext.ProjectTopics.Add(topic);
        await dbContext.SaveChangesAsync(cancellationToken);

        return topic.ToDto();
    }

    public async Task<ProjectTopicDto> UpdateAsync(Guid projectId, Guid topicId, UpdateProjectTopicRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireRoleAtLeastAsync(projectId, userId, ProjectRole.Editor, cancellationToken);

        var topic = await dbContext.ProjectTopics
            .FirstOrDefaultAsync(t => t.Id == topicId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Project group was not found.");

        var name = RequireText(request.Name, "Name", 128);
        var normalizedName = TextNormalizer.NormalizeName(name);

        if (!string.Equals(topic.NormalizedName, normalizedName, StringComparison.Ordinal) &&
            await dbContext.ProjectTopics.AnyAsync(
                t => t.ProjectId == projectId && t.NormalizedName == normalizedName && t.Id != topicId, cancellationToken))
        {
            throw new ValidationException("A group with this name already exists in the project.");
        }

        topic.Name = name;
        topic.NormalizedName = normalizedName;
        topic.Description = CleanOptional(request.Description, 512);
        topic.SortOrder = request.SortOrder;
        if (request.IsArchived.HasValue)
        {
            topic.IsArchived = request.IsArchived.Value;
        }

        topic.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return topic.ToDto();
    }

    public async Task DeleteAsync(Guid projectId, Guid topicId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireRoleAtLeastAsync(projectId, userId, ProjectRole.Owner, cancellationToken);

        var topic = await dbContext.ProjectTopics
            .FirstOrDefaultAsync(t => t.Id == topicId && t.ProjectId == projectId, cancellationToken)
            ?? throw new NotFoundException("Project group was not found.");

        dbContext.ProjectTopics.Remove(topic);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireMembershipAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        if (!await dbContext.ProjectMembers.AnyAsync(
                m => m.ProjectId == projectId && m.UserId == userId, cancellationToken))
        {
            throw new NotFoundException("Project was not found.");
        }
    }

    private async Task RequireRoleAtLeastAsync(Guid projectId, Guid userId, ProjectRole minimum, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        if (member is null)
        {
            throw new NotFoundException("Project was not found.");
        }

        var permitted = minimum == ProjectRole.Owner
            ? member.Role is ProjectRole.Owner or ProjectRole.Admin
            : member.Role is ProjectRole.Owner or ProjectRole.Admin or ProjectRole.Editor;
        if (!permitted)
        {
            throw new ForbiddenException("You do not have permission to perform this action.");
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
