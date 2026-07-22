using Microsoft.Data.Sqlite;

var dbPath = @"d:/AI/Projects/KnowledgeVault/src/KnowledgeVault/KnowledgeVault/knowledge-vault.db";
using var conn = new SqliteConnection($"Data Source={dbPath}");
conn.Open();

void Dump(string table)
{
    System.Console.WriteLine($"--- {table} ---");
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
    using var r = cmd.ExecuteReader();
    while (r.Read())
        System.Console.WriteLine($"  {r.GetString(1)} {r.GetString(2)} null={r.GetInt32(3)}");
}

foreach (var t in new[] { "KnowledgeItems", "Folders", "__EFMigrationsHistory" })
{
    try { Dump(t); } catch (Exception ex) { System.Console.WriteLine($"{t}: {ex.Message}"); }
}
