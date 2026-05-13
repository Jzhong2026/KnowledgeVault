using KnowledgeVault.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.Providers.DependencyInjection;

public static class ProviderServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeVaultProviders(this IServiceCollection services)
    {
        services.AddScoped<IAuthProvider, AuthProvider>();
        services.AddScoped<ICategoryProvider, CategoryProvider>();
        services.AddScoped<ITagProvider, TagProvider>();
        services.AddScoped<IKnowledgeItemProvider, KnowledgeItemProvider>();
        services.AddSingleton<ILookupProvider, LookupProvider>();

        return services;
    }
}
