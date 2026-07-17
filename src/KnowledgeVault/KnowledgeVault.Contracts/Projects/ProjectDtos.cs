using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Contracts.Projects;

public sealed record ProjectSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsArchived,
    ProjectRole CurrentUserRole,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record ProjectMemberDto(
    Guid UserId,
    string UserName,
    string Email,
    ProjectRole Role,
    DateTimeOffset JoinedAt);

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerUserId,
    bool IsArchived,
    ProjectRole CurrentUserRole,
    IReadOnlyList<ProjectMemberDto> Members,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
