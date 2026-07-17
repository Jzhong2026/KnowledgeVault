namespace KnowledgeVault.Contracts.Security;

public interface IDocumentAccessService
{
    Task<bool> CanViewAsync(Guid documentId, CancellationToken cancellationToken);

    Task<bool> CanEditAsync(Guid documentId, CancellationToken cancellationToken);

    Task<bool> CanCommentAsync(Guid documentId, CancellationToken cancellationToken);

    Task EnsureViewAsync(Guid documentId, CancellationToken cancellationToken);

    Task EnsureEditAsync(Guid documentId, CancellationToken cancellationToken);

    Task EnsureCommentAsync(Guid documentId, CancellationToken cancellationToken);
}
