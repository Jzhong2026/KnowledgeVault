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
        services.AddScoped<IProjectTopicProvider, ProjectTopicProvider>();
        services.AddScoped<IDocumentAccessService, DocumentAccessService>();
        services.AddScoped<IDocumentProvider, DocumentProvider>();
        services.AddScoped<IRevisionProvider, RevisionProvider>();
        services.AddScoped<ICommentProvider, CommentProvider>();
        services.AddScoped<IApiKeyProvider, ApiKeyProvider>();
        services.AddSingleton<ILookupProvider, LookupProvider>();

        return services;
    }
}
