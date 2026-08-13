using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderCreator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Folders",
                type: "TEXT",
                nullable: true);

            // Personal folders already stored their creator as OwnerUserId.
            // Project folders did not preserve creator identity historically,
            // so leave those null rather than inventing attribution.
            migrationBuilder.Sql("""
                UPDATE "Folders"
                SET "CreatedByUserId" = "OwnerUserId"
                WHERE "Scope" = 0 AND "OwnerUserId" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Folders_CreatedByUserId",
                table: "Folders",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Folders_Users_CreatedByUserId",
                table: "Folders",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Folders_Users_CreatedByUserId",
                table: "Folders");

            migrationBuilder.DropIndex(
                name: "IX_Folders_CreatedByUserId",
                table: "Folders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Folders");
        }
    }
}
