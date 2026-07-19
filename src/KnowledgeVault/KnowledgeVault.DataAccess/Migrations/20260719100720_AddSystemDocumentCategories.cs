using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemDocumentCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                INSERT INTO "Categories"
                    ("Id", "Name", "NormalizedName", "Description", "Color", "SortOrder", "IsArchived", "IsSystem", "CreatedAt", "UpdatedAt")
                SELECT '10000000-0000-0000-0000-000000000001', 'Memory', 'MEMORY',
                       'Shared durable context maintained through project review.', '#0f766e', -300, 0, 1, 1784455654355, NULL
                WHERE NOT EXISTS (SELECT 1 FROM "Categories" WHERE "NormalizedName" = 'MEMORY');

                INSERT INTO "Categories"
                    ("Id", "Name", "NormalizedName", "Description", "Color", "SortOrder", "IsArchived", "IsSystem", "CreatedAt", "UpdatedAt")
                SELECT '10000000-0000-0000-0000-000000000002', 'Task', 'TASK',
                       'Task definitions, execution notes, and delivery tracking.', '#2563eb', -200, 0, 1, 1784455654355, NULL
                WHERE NOT EXISTS (SELECT 1 FROM "Categories" WHERE "NormalizedName" = 'TASK');

                INSERT INTO "Categories"
                    ("Id", "Name", "NormalizedName", "Description", "Color", "SortOrder", "IsArchived", "IsSystem", "CreatedAt", "UpdatedAt")
                SELECT '10000000-0000-0000-0000-000000000003', 'Design', 'DESIGN',
                       'Architecture, product, and implementation design documents.', '#7c3aed', -100, 0, 1, 1784455654355, NULL
                WHERE NOT EXISTS (SELECT 1 FROM "Categories" WHERE "NormalizedName" = 'DESIGN');

                UPDATE "Categories"
                SET "Name" = 'Memory', "Description" = 'Shared durable context maintained through project review.',
                    "Color" = '#0f766e', "SortOrder" = -300, "IsArchived" = 0, "IsSystem" = 1
                WHERE "NormalizedName" = 'MEMORY';

                UPDATE "Categories"
                SET "Name" = 'Task', "Description" = 'Task definitions, execution notes, and delivery tracking.',
                    "Color" = '#2563eb', "SortOrder" = -200, "IsArchived" = 0, "IsSystem" = 1
                WHERE "NormalizedName" = 'TASK';

                UPDATE "Categories"
                SET "Name" = 'Design', "Description" = 'Architecture, product, and implementation design documents.',
                    "Color" = '#7c3aed', "SortOrder" = -100, "IsArchived" = 0, "IsSystem" = 1
                WHERE "NormalizedName" = 'DESIGN';

                UPDATE "KnowledgeItems"
                SET "CategoryId" = (SELECT "Id" FROM "Categories" WHERE "NormalizedName" = 'MEMORY' LIMIT 1)
                WHERE "DocumentType" = 3;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "KnowledgeItems"
                SET "CategoryId" = NULL
                WHERE "CategoryId" IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003');

                DELETE FROM "Categories"
                WHERE "Id" IN (
                    '10000000-0000-0000-0000-000000000001',
                    '10000000-0000-0000-0000-000000000002',
                    '10000000-0000-0000-0000-000000000003');

                UPDATE "Categories"
                SET "IsSystem" = 0
                WHERE "NormalizedName" IN ('MEMORY', 'TASK', 'DESIGN');
                """);

            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "Categories");
        }
    }
}
