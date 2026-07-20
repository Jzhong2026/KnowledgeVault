using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.DataAccess.DependencyInjection;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeVaultDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeVaultDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:KnowledgeVaultDb is not configured. Provide a PostgreSQL connection string via appsettings, user secrets, or the ConnectionStrings__KnowledgeVaultDb environment variable.");
        }

        services.AddDbContext<KnowledgeVaultDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        return services;
    }
}
