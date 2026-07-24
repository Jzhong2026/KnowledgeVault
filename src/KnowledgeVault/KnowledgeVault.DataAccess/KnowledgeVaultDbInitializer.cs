using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.DataAccess;

public static class KnowledgeVaultDbInitializer
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeVaultDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var environment = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? "Production";

        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            // Dev-only: build the schema directly from the EF model instead of
            // applying migrations. This avoids a broken EF Core SQLite table
            // rebuild that is emitted when adding columns to KnowledgeItems (the
            // AddFolderHierarchy migration recreates the table and references a
            // column that the generated temp table omits). Production keeps
            // MigrateAsync. NOTE: migration seed data (system Categories) is not
            // inserted by EnsureCreated; reseed if tests require it.
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
