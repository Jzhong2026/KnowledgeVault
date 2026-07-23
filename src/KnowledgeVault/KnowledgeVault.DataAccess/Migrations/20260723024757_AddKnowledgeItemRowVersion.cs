using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeItemRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "KnowledgeItems",
                type: "BLOB",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "KnowledgeItems");
        }
    }
}
