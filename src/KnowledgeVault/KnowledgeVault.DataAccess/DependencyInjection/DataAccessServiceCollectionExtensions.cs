using Microsoft.Data.Sqlite;
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
            ?? "Data Source=knowledge-vault.db";
        connectionString = ResolveSqliteConnectionString(connectionString, contentRootPath);

        services.AddDbContext<KnowledgeVaultDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        return services;
    }

    private static string ResolveSqliteConnectionString(string connectionString, string? contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;

        if (string.IsNullOrWhiteSpace(dataSource)
            || dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(dataSource))
        {
            return connectionString;
        }

        var rootPath = string.IsNullOrWhiteSpace(contentRootPath)
            ? AppContext.BaseDirectory
            : contentRootPath;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, dataSource));
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        builder.DataSource = fullPath;
        return builder.ConnectionString;
    }
}
