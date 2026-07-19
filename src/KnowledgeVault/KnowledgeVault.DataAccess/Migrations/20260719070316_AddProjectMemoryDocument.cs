using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectMemoryDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_ProjectId_ProjectMemory",
                table: "KnowledgeItems",
                column: "ProjectId",
                unique: true,
                filter: "[DocumentType] = 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_ProjectId_ProjectMemory",
                table: "KnowledgeItems");
        }
    }
}
