using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SystemLevelCategoriesTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_Tags_Users_UserId",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Tags_UserId_NormalizedName",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_NormalizedName",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Tags");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Categories");

            // Merge previously per-user rows into system-level unique rows.
            // Keep the lowest Id per NormalizedName, remap references, then delete duplicates.
            migrationBuilder.Sql(
                """
                IF OBJECT_ID('Categories') IS NOT NULL
                BEGIN
                    UPDATE ki SET ki.CategoryId = k.KeepId
                    FROM KnowledgeItems ki
                    INNER JOIN Categories c ON ki.CategoryId = c.Id
                    INNER JOIN (
                        SELECT NormalizedName, MIN(Id) AS KeepId
                        FROM Categories
                        WHERE NormalizedName IS NOT NULL
                        GROUP BY NormalizedName
                        HAVING COUNT(*) > 1
                    ) k ON c.NormalizedName = k.NormalizedName
                    WHERE ki.CategoryId <> k.KeepId;

                    WITH Dups AS (
                        SELECT Id, ROW_NUMBER() OVER (PARTITION BY NormalizedName ORDER BY Id) AS rn
                        FROM Categories
                        WHERE NormalizedName IS NOT NULL
                    )
                    DELETE FROM Categories WHERE Id IN (SELECT Id FROM Dups WHERE rn > 1);
                END
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID('Tags') IS NOT NULL
                BEGIN
                    DELETE kit
                    FROM KnowledgeItemTags kit
                    INNER JOIN Tags t ON kit.TagId = t.Id
                    INNER JOIN (
                        SELECT NormalizedName, MIN(Id) AS KeepId
                        FROM Tags
                        WHERE NormalizedName IS NOT NULL
                        GROUP BY NormalizedName
                        HAVING COUNT(*) > 1
                    ) k ON t.NormalizedName = k.NormalizedName
                    WHERE EXISTS (
                        SELECT 1 FROM KnowledgeItemTags kit2
                        WHERE kit2.KnowledgeItemId = kit.KnowledgeItemId
                          AND kit2.TagId = k.KeepId
                    );

                    UPDATE kit SET kit.TagId = k.KeepId
                    FROM KnowledgeItemTags kit
                    INNER JOIN Tags t ON kit.TagId = t.Id
                    INNER JOIN (
                        SELECT NormalizedName, MIN(Id) AS KeepId
                        FROM Tags
                        WHERE NormalizedName IS NOT NULL
                        GROUP BY NormalizedName
                        HAVING COUNT(*) > 1
                    ) k ON t.NormalizedName = k.NormalizedName
                    WHERE kit.TagId <> k.KeepId;

                    WITH Dups AS (
                        SELECT Id, ROW_NUMBER() OVER (PARTITION BY NormalizedName ORDER BY Id) AS rn
                        FROM Tags
                        WHERE NormalizedName IS NOT NULL
                    )
                    DELETE FROM Tags WHERE Id IN (SELECT Id FROM Dups WHERE rn > 1);
                END
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Tags",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Tags_UserId_NormalizedName",
                table: "Tags",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_NormalizedName",
                table: "Categories",
                columns: new[] { "UserId", "NormalizedName" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Users_UserId",
                table: "Categories",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Tags_Users_UserId",
                table: "Tags",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
