using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class ProjectMemoryCandidateProvider(
    KnowledgeVaultDbContext dbContext,
    ICurrentUserContext currentUserContext,
    IDateTimeProvider dateTimeProvider,
    IProjectMemoryProvider projectMemoryProvider) : IProjectMemoryCandidateProvider
{
    private const int MaxProposedContentLength = 16_000;

    public async Task<IReadOnlyList<ProjectMemoryCandidateDto>> ListAsync(
        Guid projectId,
        bool includeResolved,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        _ = await RequireMemberRoleAsync(projectId, userId, cancellationToken);

        var query = BuildCandidateQuery().Where(x => x.ProjectId == projectId);
        if (!includeResolved)
        {
            query = query.Where(x => x.Status == ProjectMemoryCandidateStatus.Pending);
        }

        var candidates = await query
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return candidates.Select(ToDto).ToArray();
    }

    public async Task<ProjectMemoryCandidateDto> CreateAsync(
        Guid projectId,
        CreateProjectMemoryCandidateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        _ = await RequireMemberRoleAsync(projectId, userId, cancellationToken);
        var memoryId = await projectMemoryProvider.EnsureExistsAsync(projectId, cancellationToken);
        var currentRevisionNumber = await dbContext.KnowledgeItems
            .Where(x => x.Id == memoryId)
            .Select(x => x.CurrentRevisionNumber)
            .SingleAsync(cancellationToken);

        var candidate = new ProjectMemoryCandidate
        {
            ProjectId = projectId,
            TargetSection = request.TargetSection,
            ProposedContent = RequireText(
                request.ProposedContent,
                "Proposed content",
                MaxProposedContentLength),
            Rationale = CleanOptional(request.Rationale, 1024),
            Status = ProjectMemoryCandidateStatus.Pending,
            ProposedByUserId = userId,
            MemoryRevisionAtProposal = currentRevisionNumber,
            CreatedAt = dateTimeProvider.UtcNow
        };

        dbContext.ProjectMemoryCandidates.Add(candidate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(candidate.Id, cancellationToken);
    }

    public async Task<ProjectMemoryCandidateDto> AcceptAsync(
        Guid projectId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var role = await RequireMemberRoleAsync(projectId, userId, cancellationToken);
        RequireAdministrator(role);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var candidate = await dbContext.ProjectMemoryCandidates
            .FirstOrDefaultAsync(
                x => x.Id == candidateId && x.ProjectId == projectId,
                cancellationToken)
            ?? throw new NotFoundException("Memory candidate was not found.");

        RequirePending(candidate);

        var memoryId = await projectMemoryProvider.EnsureExistsAsync(projectId, cancellationToken);
        var memory = await dbContext.KnowledgeItems
            .Include(x => x.CurrentRevision)
            .FirstAsync(x => x.Id == memoryId, cancellationToken);
        var currentRevision = memory.CurrentRevision
            ?? throw new ConflictException("MEMORY.md does not have a current revision.");
        var now = dateTimeProvider.UtcNow;
        var nextRevisionNumber = memory.CurrentRevisionNumber + 1;
        var nextRevision = new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = memory.Id,
            RevisionNumber = nextRevisionNumber,
            Title = currentRevision.Title,
            Summary = currentRevision.Summary,
            Content = AppendToSection(
                currentRevision.Content,
                candidate.TargetSection,
                candidate.ProposedContent),
            SourceUrl = currentRevision.SourceUrl,
            LinkDisplayText = currentRevision.LinkDisplayText,
            LinkUrl = currentRevision.LinkUrl,
            ChangeNote = $"Accepted memory candidate {candidate.Id}.",
            CreatedByUserId = userId,
            CreatedAt = now
        };

        memory.CurrentRevisionId = nextRevision.Id;
        memory.CurrentRevisionNumber = nextRevisionNumber;
        memory.UpdatedAt = now;
        candidate.Status = ProjectMemoryCandidateStatus.Accepted;
        candidate.ReviewedByUserId = userId;
        candidate.ReviewedAt = now;
        candidate.AppliedMemoryRevisionNumber = nextRevisionNumber;
        candidate.UpdatedAt = now;

        dbContext.KnowledgeItemRevisions.Add(nextRevision);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException(
                "MEMORY.md changed while the candidate was being accepted. Review and retry.");
        }

        return await ReloadAsync(candidate.Id, cancellationToken);
    }

    public async Task<ProjectMemoryCandidateDto> CancelAsync(
        Guid projectId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUser();
        var role = await RequireMemberRoleAsync(projectId, userId, cancellationToken);
        RequireAdministrator(role);

        var candidate = await dbContext.ProjectMemoryCandidates
            .FirstOrDefaultAsync(
                x => x.Id == candidateId && x.ProjectId == projectId,
                cancellationToken)
            ?? throw new NotFoundException("Memory candidate was not found.");

        RequirePending(candidate);
        var now = dateTimeProvider.UtcNow;
        candidate.Status = ProjectMemoryCandidateStatus.Cancelled;
        candidate.ReviewedByUserId = userId;
        candidate.ReviewedAt = now;
        candidate.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(candidate.Id, cancellationToken);
    }

    internal static string AppendToSection(
        string markdown,
        ProjectMemorySection section,
        string proposedContent)
    {
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
        var heading = $"## {GetSectionHeading(section)}";
        var lines = normalized.Split('\n').ToList();
        var headingIndex = lines.FindIndex(line =>
            string.Equals(line.Trim(), heading, StringComparison.Ordinal));

        if (headingIndex < 0)
        {
            var maintenanceIndex = lines.FindIndex(line =>
                string.Equals(line.Trim(), "## Maintenance Rules", StringComparison.Ordinal));
            var insertIndex = maintenanceIndex >= 0 ? maintenanceIndex : lines.Count;
            var newSection = new List<string> { heading, string.Empty };
            newSection.AddRange(proposedContent.Trim().Split('\n'));
            newSection.Add(string.Empty);
            lines.InsertRange(insertIndex, newSection);
            return string.Join("\n", lines).TrimEnd() + "\n";
        }

        var nextHeadingIndex = lines.FindIndex(
            headingIndex + 1,
            line => line.StartsWith("## ", StringComparison.Ordinal));
        var insertionIndex = nextHeadingIndex >= 0 ? nextHeadingIndex : lines.Count;

        while (insertionIndex > headingIndex + 1 &&
               string.IsNullOrWhiteSpace(lines[insertionIndex - 1]))
        {
            insertionIndex--;
        }

        var addition = new List<string> { string.Empty };
        addition.AddRange(proposedContent.Trim().Split('\n'));
        addition.Add(string.Empty);
        lines.InsertRange(insertionIndex, addition);
        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    private static string GetSectionHeading(ProjectMemorySection section)
    {
        return section switch
        {
            ProjectMemorySection.ProjectPurpose => "Project Purpose",
            ProjectMemorySection.CurrentContext => "Current Context",
            ProjectMemorySection.ConstraintsAndConventions => "Constraints and Conventions",
            ProjectMemorySection.KeyDecisions => "Key Decisions",
            ProjectMemorySection.ImportantLocationsAndCommands => "Important Locations and Commands",
            ProjectMemorySection.AgentPrompts => "Agent Prompts",
            ProjectMemorySection.AgentHandoff => "Agent Handoff",
            ProjectMemorySection.OpenQuestions => "Open Questions",
            _ => throw new ValidationException("The target memory section is invalid.")
        };
    }

    private async Task<ProjectRole> RequireMemberRoleAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var role = await dbContext.ProjectMembers
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.UserId == userId)
            .Select(x => (ProjectRole?)x.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role ?? throw new NotFoundException("Project was not found.");
    }

    private static void RequireAdministrator(ProjectRole role)
    {
        if (role is not (ProjectRole.Owner or ProjectRole.Admin))
        {
            throw new ForbiddenException("Only a project owner or administrator can review memory candidates.");
        }
    }

    private static void RequirePending(ProjectMemoryCandidate candidate)
    {
        if (candidate.Status != ProjectMemoryCandidateStatus.Pending)
        {
            throw new ConflictException("The memory candidate has already been reviewed.");
        }
    }

    private IQueryable<ProjectMemoryCandidate> BuildCandidateQuery()
    {
        return dbContext.ProjectMemoryCandidates
            .AsNoTracking()
            .Include(x => x.ProposedByUser)
            .Include(x => x.ReviewedByUser);
    }

    private async Task<ProjectMemoryCandidateDto> ReloadAsync(
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var candidate = await BuildCandidateQuery()
            .FirstAsync(x => x.Id == candidateId, cancellationToken);
        return ToDto(candidate);
    }

    private static ProjectMemoryCandidateDto ToDto(ProjectMemoryCandidate candidate)
    {
        return new ProjectMemoryCandidateDto(
            candidate.Id,
            candidate.ProjectId,
            candidate.TargetSection,
            candidate.ProposedContent,
            candidate.Rationale,
            candidate.Status,
            candidate.ProposedByUserId,
            GetDisplayName(candidate.ProposedByUser),
            candidate.MemoryRevisionAtProposal,
            candidate.ReviewedByUserId,
            candidate.ReviewedByUser is null ? null : GetDisplayName(candidate.ReviewedByUser),
            candidate.ReviewedAt,
            candidate.AppliedMemoryRevisionNumber,
            candidate.CreatedAt);
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
        return string.IsNullOrWhiteSpace(value)
            ? null
            : RequireText(value, "Rationale", maxLength);
    }

    private static string GetDisplayName(User? user)
    {
        return user?.Nickname ?? user?.UserName ?? string.Empty;
    }
}
