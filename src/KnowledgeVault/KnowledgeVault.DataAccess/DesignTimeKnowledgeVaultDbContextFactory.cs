using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace KnowledgeVault.DataAccess;

public sealed class DesignTimeKnowledgeVaultDbContextFactory : IDesignTimeDbContextFactory<KnowledgeVaultDbContext>
{
    public KnowledgeVaultDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("KnowledgeVaultDb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:KnowledgeVaultDb is not configured. Provide it via appsettings, user secrets, or the ConnectionStrings__KnowledgeVaultDb environment variable.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeVaultDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new KnowledgeVaultDbContext(optionsBuilder.Options);
    }
}
