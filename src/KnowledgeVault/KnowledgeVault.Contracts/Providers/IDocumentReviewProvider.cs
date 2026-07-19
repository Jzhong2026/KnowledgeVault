using KnowledgeVault.Contracts.Common;
using KnowledgeVault.Contracts.Reviews;

namespace KnowledgeVault.Contracts.Providers;

public interface IDocumentReviewProvider
{
    Task<IReadOnlyList<DocumentReviewDto>> CreateAsync(
        Guid documentId,
        CreateDocumentReviewRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<DocumentReviewDto>> ListAsync(
        DocumentReviewQuery query,
        CancellationToken cancellationToken);

    Task<DocumentReviewContextDto> GetContextAsync(
        Guid documentId,
        int revisionNumber,
        CancellationToken cancellationToken);

    Task<DocumentReviewDto> DecideAsync(
        Guid reviewId,
        DecideDocumentReviewRequest request,
        CancellationToken cancellationToken);

    Task<DocumentReviewDto> CancelAsync(Guid reviewId, CancellationToken cancellationToken);
}
