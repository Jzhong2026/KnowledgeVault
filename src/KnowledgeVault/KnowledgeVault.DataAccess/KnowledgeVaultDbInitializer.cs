using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeVault.DataAccess;

public static class KnowledgeVaultDbInitializer
{
    public static async Task MigrateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeVaultDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
