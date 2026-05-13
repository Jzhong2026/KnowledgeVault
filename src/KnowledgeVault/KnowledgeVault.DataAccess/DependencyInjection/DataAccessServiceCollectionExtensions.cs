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
        var connectionString = configuration.GetConnectionString("KnowledgeVaultDb")
            ?? "Data Source=knowledge-vault.db";

        services.AddDbContext<KnowledgeVaultDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        return services;
    }
}
