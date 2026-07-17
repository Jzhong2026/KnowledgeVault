using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;

namespace KnowledgeVault.Providers;

public static class DocumentQueryExtensions
{
    public static IQueryable<KnowledgeItem> ApplyDocumentSort(this IQueryable<KnowledgeItem> query, DocumentSort sort)
    {
        return sort switch
        {
            DocumentSort.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            DocumentSort.TitleAsc => query.OrderBy(x => x.CurrentRevision != null ? x.CurrentRevision.Title : string.Empty).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).ThenBy(x => x.Id),
        };
    }
}
