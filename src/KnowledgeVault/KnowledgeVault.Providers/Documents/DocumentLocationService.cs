using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

/// <summary>
/// Resolves and validates where a document may live: its project/topic,
/// its folder, and its category. Extracted from DocumentProvider so the
/// write paths only orchestrate.
/// </summary>
public sealed class DocumentLocationService(
    KnowledgeVaultDbContext dbContext,
    ProjectAccessService projectAccess)
{
    /// <summary>
    /// Validates the scope/project/topic combination and returns the resolved pair.
    /// Personal documents must not reference a project or topic; project documents
    /// require an active project the user can edit content in.
    /// </summary>
    public async Task<(Guid? ProjectId, Guid? TopicId)> ResolveLocationAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? topicId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (scope == DocumentScope.Personal)
        {
            if (projectId is not null || topicId is not null)
            {
                throw new ValidationException("Personal documents cannot be assigned to a project or group.");
            }

            return (null, null);
        }

        if (projectId is null || projectId == Guid.Empty)
        {
            throw new ValidationException("Project is required for project documents.");
        }

        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(x => x.Id == projectId.Value && !x.IsArchived, cancellationToken);
        if (!projectExists)
        {
            throw new ValidationException("Project is invalid or archived.");
        }

        await projectAccess.EnsureContentEditorAsync(
            projectId.Value,
            userId,
            "You do not have permission to create or move documents in this project.",
            cancellationToken);

        if (topicId is not null)
        {
            var topicExists = await dbContext.ProjectTopics
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == topicId.Value && x.ProjectId == projectId.Value && !x.IsArchived,
                    cancellationToken);
            if (!topicExists)
            {
                throw new ValidationException("Group is invalid, archived, or belongs to another project.");
            }
        }

        return (projectId, topicId);
    }

    /// <summary>
    /// Validates that the target folder exists, matches the document's scope and
    /// project, and that the user may place documents in it. Null folderId means
    /// "no folder" and returns null.
    /// </summary>
    public async Task<Folder?> ResolveFolderAsync(
        DocumentScope scope,
        Guid? projectId,
        Guid? folderId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (folderId is null)
        {
            return null;
        }

        var folder = await dbContext.Folders.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken)
            ?? throw new ValidationException("The target folder does not exist.");

        if (folder.Scope != scope)
        {
            throw new ValidationException("The target folder does not match the document scope.");
        }

        if (scope == DocumentScope.Project)
        {
            if (folder.ProjectId != projectId)
            {
                throw new ValidationException("The target folder does not belong to this project.");
            }

            await projectAccess.EnsureContentEditorAsync(
                projectId,
                userId,
                "You do not have permission to place documents in this folder.",
                cancellationToken);
        }
        else if (folder.OwnerUserId != userId)
        {
            throw new ForbiddenException("You do not have permission to place documents in this folder.");
        }

        return folder;
    }

    /// <summary>Throws when the category id points to a missing or archived category.</summary>
    public async Task EnsureCategoryAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await dbContext.Categories.AnyAsync(
            x => x.Id == categoryId.Value && !x.IsArchived,
            cancellationToken);

        if (!exists)
        {
            throw new ValidationException("Category is invalid or archived.");
        }
    }
}
