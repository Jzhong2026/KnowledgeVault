using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KnowledgeVault.DataAccess;

public sealed class DesignTimeKnowledgeVaultDbContextFactory : IDesignTimeDbContextFactory<KnowledgeVaultDbContext>
{
    public KnowledgeVaultDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeVaultDbContext>();
        optionsBuilder.UseSqlite("Data Source=knowledge-vault.db");

        return new KnowledgeVaultDbContext(optionsBuilder.Options);
    }
}
