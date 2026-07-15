using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.DataAccess.DependencyInjection;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeVaultDataAccess(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRootPath = null)
    {
        var connectionString = configuration.GetConnectionString("KnowledgeVaultDb")
            ?? "Data Source=(local)\\SqlExpress;Initial Catalog=KnowledgeVault;User ID=Jzhong1985;Password=Jasonzhong1985@;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;";

        services.AddDbContext<KnowledgeVaultDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        return services;
    }
}
