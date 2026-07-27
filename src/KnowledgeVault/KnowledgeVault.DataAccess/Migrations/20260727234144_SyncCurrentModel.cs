using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The preceding AddFolderArchiveState migration already performs the
            // required schema change. This migration only aligns EF's snapshot
            // with the current model so EF Core 10 accepts subsequent data
            // migrations; applying a second ALTER TABLE would fail on SQLite.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Snapshot-only migration; no data or schema is reversed here.
        }
    }
}
