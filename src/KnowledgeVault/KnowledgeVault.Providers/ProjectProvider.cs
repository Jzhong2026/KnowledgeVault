using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Projects;
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

public sealed class ProjectProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IProjectMemoryProvider projectMemoryProvider) : IProjectProvider
{
    public async Task<PagedResult<ProjectSummaryDto>> ListAsync(ProjectQuery query, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Projects are private: only list projects the caller is a member of.
        // FollowingOnly is now implied, but is kept for API compatibility.
        var baseQuery = dbContext.Projects
            .AsNoTracking()
            .Where(p => (query.IncludeArchived || !p.IsArchived) &&
                        p.Members.Any(m => m.UserId == userId));

        if (query.FollowingOnly)
        {
            baseQuery = baseQuery.Where(p => p.Members.Any(m => m.UserId == userId));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(p =>
                p.Name.Contains(search) ||
                (p.Description != null && p.Description.Contains(search)));
        }

        var totalCount = await baseQuery.CountAsync(cancellationToken);
        var projects = await baseQuery
            .Include(p => p.Members)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = projects.Select(p =>
        {
            var role = p.Members.FirstOrDefault(m => m.UserId == userId)?.Role;
            return new ProjectSummaryDto(
                p.Id,
                p.Name,
                p.Description,
                p.IsArchived,
                role,
                role.HasValue,
                p.Members.Count,
                p.CreatedAt,
                p.UpdatedAt);
        }).ToArray();

        return new PagedResult<ProjectSummaryDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProjectDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var role = project.Members.FirstOrDefault(m => m.UserId == userId)?.Role;
        if (role is null)
        {
            // Projects are private: a non-member must not be able to read the
            // project or its member list (which includes emails).
            throw new NotFoundException("Project was not found.");
        }

        return project.ToDto(role);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var now = dateTimeProvider.UtcNow;

        var project = new Project
        {
            Name = RequireText(request.Name, "Name", 128),
            Description = CleanOptional(request.Description, 512),
            OwnerUserId = userId,
            IsArchived = false,
            CreatedAt = now,
            Members =
            [
                new ProjectMember
                {
                    UserId = userId,
                    Role = ProjectRole.Owner,
                    CreatedAt = now
                }
            ]
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        await projectMemoryProvider.EnsureExistsAsync(project.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await ReloadAsync(project.Id, userId, cancellationToken);
    }

    public async Task<ProjectDto> UpdateAsync(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        RequireOwner(project, userId);

        project.Name = RequireText(request.Name, "Name", 128);
        project.Description = CleanOptional(request.Description, 512);
        if (request.IsArchived.HasValue)
        {
            project.IsArchived = request.IsArchived.Value;
        }

        project.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return project.ToDto(ProjectRole.Owner);
    }

    public async Task DeleteAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        RequireOwner(project, userId);

        var memoryDocuments = await dbContext.KnowledgeItems
            .Where(x => x.ProjectId == projectId && x.DocumentType == DocumentType.ProjectMemory)
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var memoryDocument in memoryDocuments)
        {
            memoryDocument.CurrentRevisionId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.KnowledgeItems.RemoveRange(memoryDocuments);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Projects.Remove(project);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProjectDto> FollowAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        if (project.IsArchived)
        {
            throw new ValidationException("Archived projects cannot be followed.");
        }

        var existing = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (existing is null)
        {
            var member = new ProjectMember
            {
                ProjectId = projectId,
                UserId = userId,
                Role = ProjectRole.Editor,
                CreatedAt = dateTimeProvider.UtcNow
            };
            dbContext.ProjectMembers.Add(member);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await ReloadAsync(projectId, userId, cancellationToken);
    }

    public async Task UnfollowAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var member = await dbContext.ProjectMembers
            .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        if (member is null)
        {
            return;
        }

        if (member.Role == ProjectRole.Owner)
        {
            throw new ValidationException("Project owners cannot unfollow a project they own.");
        }

        dbContext.ProjectMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        await RequireMembershipAsync(projectId, userId, cancellationToken);

        var members = await dbContext.ProjectMembers
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .Include(m => m.User)
            .OrderBy(m => m.Role)
            .ThenBy(m => m.User!.UserName)
            .Select(m => new ProjectMemberDto(
                m.UserId,
                m.User!.UserName,
                m.User.Email,
                m.Role,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return members;
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var administrator = RequireAdministrator(project, userId);
        ValidateAdministratorAssignment(administrator, request.Role);

        if (!await dbContext.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new ValidationException("Target user does not exist.");
        }

        if (project.Members.Any(m => m.UserId == request.UserId))
        {
            throw new ConflictException("User is already a member of this project.");
        }

        var member = new ProjectMember
        {
            ProjectId = projectId,
            UserId = request.UserId,
            Role = request.Role,
            CreatedAt = dateTimeProvider.UtcNow
        };

        dbContext.ProjectMembers.Add(member);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMemberDtoAsync(projectId, request.UserId, cancellationToken);
    }

    public async Task<ProjectMemberDto> UpdateMemberAsync(Guid projectId, Guid targetUserId, UpdateProjectMemberRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var administrator = RequireAdministrator(project, userId);

        var member = project.Members.FirstOrDefault(m => m.UserId == targetUserId)
            ?? throw new NotFoundException("Project member was not found.");

        ValidateAdministratorMemberChange(administrator, member, request.Role, userId);

        if (member.Role == ProjectRole.Owner && request.Role != ProjectRole.Owner)
        {
            EnsureRetainsOwner(project);
        }

        member.Role = request.Role;
        member.UpdatedAt = dateTimeProvider.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMemberDtoAsync(projectId, targetUserId, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var project = await dbContext.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new NotFoundException("Project was not found.");

        var administrator = RequireAdministrator(project, userId);

        var member = project.Members.FirstOrDefault(m => m.UserId == targetUserId)
            ?? throw new NotFoundException("Project member was not found.");

        ValidateAdministratorMemberChange(administrator, member, null, userId);

        if (member.Role == ProjectRole.Owner)
        {
            EnsureRetainsOwner(project);
        }

        dbContext.ProjectMembers.Remove(member);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RequireMembershipAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await dbContext.ProjectMembers
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, cancellationToken);

        if (!isMember)
        {
            throw new NotFoundException("Project was not found.");
        }
    }

    private static void EnsureRetainsOwner(Project project)
    {
        var ownerCount = project.Members.Count(m => m.Role == ProjectRole.Owner);
        if (ownerCount <= 1)
        {
            throw new ValidationException("A project must retain at least one owner.");
        }
    }

    private static ProjectRole GetRoleOrThrow(Project project, Guid userId)
    {
        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null)
        {
            throw new NotFoundException("Project was not found.");
        }

        return member.Role;
    }

    private static void RequireOwner(Project project, Guid userId)
    {
        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null || member.Role != ProjectRole.Owner)
        {
            throw new ForbiddenException("Only the project owner can perform this action.");
        }
    }

    private static ProjectMember RequireAdministrator(Project project, Guid userId)
    {
        var member = project.Members.FirstOrDefault(m => m.UserId == userId);
        if (member is null || member.Role is not (ProjectRole.Owner or ProjectRole.Admin))
        {
            throw new ForbiddenException("Only a project owner or administrator can perform this action.");
        }

        return member;
    }

    private static void ValidateAdministratorAssignment(
        ProjectMember administrator,
        ProjectRole requestedRole)
    {
        if (!Enum.IsDefined(requestedRole))
        {
            throw new ValidationException("Project role is invalid.");
        }

        if (administrator.Role == ProjectRole.Admin && requestedRole == ProjectRole.Owner)
        {
            throw new ForbiddenException("Administrators cannot assign the project owner role.");
        }
    }

    private static void ValidateAdministratorMemberChange(
        ProjectMember administrator,
        ProjectMember target,
        ProjectRole? requestedRole,
        Guid currentUserId)
    {
        if (requestedRole.HasValue && !Enum.IsDefined(requestedRole.Value))
        {
            throw new ValidationException("Project role is invalid.");
        }

        if (administrator.Role != ProjectRole.Admin)
        {
            return;
        }

        if (target.UserId == currentUserId)
        {
            throw new ForbiddenException("Administrators cannot change their own membership.");
        }

        if (target.Role == ProjectRole.Owner || requestedRole == ProjectRole.Owner)
        {
            throw new ForbiddenException("Administrators cannot change the project owner role.");
        }
    }

    private async Task<ProjectDto> ReloadAsync(Guid projectId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Members)
            .ThenInclude(m => m.User)
            .FirstAsync(p => p.Id == projectId, cancellationToken);

        return project.ToDto(GetRoleOrThrow(project, userId));
    }

    private async Task<ProjectMemberDto> GetMemberDtoAsync(Guid projectId, Guid targetUserId, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers
            .AsNoTracking()
            .Include(m => m.User)
            .FirstAsync(m => m.ProjectId == projectId && m.UserId == targetUserId, cancellationToken);

        return new ProjectMemberDto(
            member.UserId,
            member.User?.UserName ?? string.Empty,
            member.User?.Email ?? string.Empty,
            member.Role,
            member.CreatedAt);
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
