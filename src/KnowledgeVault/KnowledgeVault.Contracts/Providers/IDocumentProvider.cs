using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Documents;

namespace KnowledgeVault.Contracts.Providers;

public interface IDocumentProvider
{
    Task<PagedResult<KnowledgeItemSummaryDto>> ListAsync(DocumentQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentOwnerDto>> ListOwnersAsync(Guid? projectId, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> CreateAsync(CreateDocumentRequest request, CancellationToken cancellationToken);

    Task<KnowledgeItemDto> UpdateAsync(Guid id, UpdateDocumentRequest request, CancellationToken cancellationToken);

    Task UpdateMetadataAsync(Guid id, UpdateDocumentMetadataRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public interface IRevisionProvider
{
    Task<PagedResult<RevisionSummaryDto>> ListAsync(Guid documentId, int page, int pageSize, CancellationToken cancellationToken);

    Task<RevisionDto> GetAsync(Guid documentId, int revisionNumber, CancellationToken cancellationToken);
}

public interface ICommentProvider
{
    Task<PagedResult<CommentDto>> ListAsync(Guid documentId, int revisionNumber, int page, int pageSize, CancellationToken cancellationToken);

    Task<CommentDto> AddAsync(Guid documentId, int revisionNumber, AddCommentRequest request, CancellationToken cancellationToken);

    Task<CommentDto> UpdateAsync(Guid commentId, UpdateCommentRequest request, CancellationToken cancellationToken);

    Task<CommentDto> ResolveAsync(Guid commentId, ResolveCommentRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid commentId, CancellationToken cancellationToken);
}
