using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KnowledgeVault.DataAccess;

public sealed class DesignTimeKnowledgeVaultDbContextFactory : IDesignTimeDbContextFactory<KnowledgeVaultDbContext>
{
    private const string ConnectionString = "Data Source=(local)\\SqlExpress;Initial Catalog=KnowledgeVault;User ID=Jzhong1985;Password=Jasonzhong1985@;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=True;Connection Timeout=30;";

    public KnowledgeVaultDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<KnowledgeVaultDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);

        return new KnowledgeVaultDbContext(optionsBuilder.Options);
    }
}
