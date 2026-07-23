using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.Providers.DependencyInjection;

public static class ProviderServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeVaultProviders(this IServiceCollection services)
    {
        services.AddScoped<IAuthProvider, AuthProvider>();
        services.AddScoped<ICategoryProvider, CategoryProvider>();
        services.AddScoped<ITagProvider, TagProvider>();
        services.AddScoped<IProjectProvider, ProjectProvider>();
        services.AddScoped<IProjectMemoryProvider, ProjectMemoryProvider>();
        services.AddScoped<IProjectMemoryCandidateProvider, ProjectMemoryCandidateProvider>();
        services.AddScoped<IProjectTopicProvider, ProjectTopicProvider>();
        services.AddScoped<IDocumentAccessService, DocumentAccessService>();
        services.AddScoped<IFolderProvider, FolderProvider>();
        services.AddScoped<IDocumentProvider, DocumentProvider>();

        // Collaborators extracted from the providers above. Registered explicitly
        // (scoped) so a single ProjectAccessService instance is shared by
        // DocumentProvider, DocumentAccessService and FolderProvider within a scope.
        services.AddScoped<ProjectAccessService>();
        services.AddScoped<DocumentTagService>();
        services.AddScoped<DocumentLocationService>();
        services.AddScoped<IRevisionProvider, RevisionProvider>();
        services.AddScoped<ICommentProvider, CommentProvider>();
        services.AddScoped<IDocumentReviewProvider, DocumentReviewProvider>();
        services.AddScoped<IApiKeyProvider, ApiKeyProvider>();
        services.AddSingleton<ILookupProvider, LookupProvider>();

        return services;
    }
}
