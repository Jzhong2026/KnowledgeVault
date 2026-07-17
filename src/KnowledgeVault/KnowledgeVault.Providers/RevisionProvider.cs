using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Providers.Mapping;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers;

public sealed class RevisionProvider(
    KnowledgeVaultDbContext dbContext,
    IDocumentAccessService documentAccessService) : IRevisionProvider
{
    public async Task<PagedResult<RevisionSummaryDto>> ListAsync(Guid documentId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureViewAsync(documentId, cancellationToken);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var revisionsQuery = dbContext.KnowledgeItemRevisions
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Where(x => x.KnowledgeItemId == documentId);

        var totalCount = await revisionsQuery.CountAsync(cancellationToken);
        var revisions = await revisionsQuery
            .OrderByDescending(x => x.RevisionNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<RevisionSummaryDto>(
            revisions.Select(x => x.ToSummaryDto()).ToArray(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<RevisionDto> GetAsync(Guid documentId, int revisionNumber, CancellationToken cancellationToken)
    {
        await documentAccessService.EnsureViewAsync(documentId, cancellationToken);

        var revision = await dbContext.KnowledgeItemRevisions
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.KnowledgeItemId == documentId && x.RevisionNumber == revisionNumber, cancellationToken)
            ?? throw new NotFoundException("Revision was not found.");

        return revision.ToDto();
    }
}
