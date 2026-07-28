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
        migrationBuilder.AlterColumn<string>(
            name: "Content",
            table: "KnowledgeItemComments",
            type: "TEXT",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT",
            oldMaxLength: 4000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Content",
            table: "KnowledgeItemComments",
            type: "TEXT",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "TEXT");
    }
}
