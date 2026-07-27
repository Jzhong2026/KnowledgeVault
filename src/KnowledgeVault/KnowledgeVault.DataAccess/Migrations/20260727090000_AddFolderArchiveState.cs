using KnowledgeVault.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations;

[DbContext(typeof(KnowledgeVaultDbContext))]
[Migration("20260727090000_AddFolderArchiveState")]
public partial class AddFolderArchiveState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "ArchivedAt",
            table: "Folders",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            table: "Folders",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ArchivedAt", table: "Folders");
        migrationBuilder.DropColumn(name: "IsArchived", table: "Folders");
    }
}
