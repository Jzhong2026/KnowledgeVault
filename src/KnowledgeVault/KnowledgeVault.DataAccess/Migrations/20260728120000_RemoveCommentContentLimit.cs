using KnowledgeVault.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations;

[DbContext(typeof(KnowledgeVaultDbContext))]
[Migration("20260728120000_RemoveCommentContentLimit")]
public partial class RemoveCommentContentLimit : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQLite stores comment content as unbounded TEXT already. The previous
        // 4000-character limit was EF/provider validation, not a physical table
        // constraint, so this migration only records the model change safely.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Restoring model metadata does not require a SQLite schema change.
    }
}
