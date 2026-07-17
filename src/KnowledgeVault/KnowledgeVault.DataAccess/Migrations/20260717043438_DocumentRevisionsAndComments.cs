using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DocumentRevisionsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_Users_UserId",
                table: "KnowledgeItems");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_UserId_CategoryId",
                table: "KnowledgeItems");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_UserId_Status",
                table: "KnowledgeItems");

            migrationBuilder.AddColumn<string>(
                name: "Nickname",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentRevisionId",
                table: "KnowledgeItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentRevisionNumber",
                table: "KnowledgeItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "KnowledgeItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "KnowledgeItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TopicId",
                table: "KnowledgeItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnowledgeItemRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TicketNo = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TicketUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ChangeNote = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItemRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemRevisions_KnowledgeItems_KnowledgeItemId",
                        column: x => x.KnowledgeItemId,
                        principalTable: "KnowledgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeItemComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    KnowledgeItemRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemComments_KnowledgeItemRevisions_KnowledgeItemRevisionId",
                        column: x => x.KnowledgeItemRevisionId,
                        principalTable: "KnowledgeItemRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.Sql(
                """
                INSERT INTO KnowledgeItemRevisions (Id, KnowledgeItemId, RevisionNumber, Title, Summary, Content, SourceUrl, CreatedByUserId, CreatedAt, UpdatedAt)
                SELECT NEWID(), Id, 1, ISNULL(Title, ''), Summary, ISNULL(Content, ''), SourceUrl, UserId, ISNULL(CreatedAt, SYSUTCDATETIME()), NULL
                FROM KnowledgeItems;
                """);

            migrationBuilder.Sql(
                """
                UPDATE ki
                SET ki.CurrentRevisionNumber = 1,
                    ki.CurrentRevisionId = r.Id
                FROM KnowledgeItems ki
                INNER JOIN KnowledgeItemRevisions r ON r.KnowledgeItemId = ki.Id AND r.RevisionNumber = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_CurrentRevisionId",
                table: "KnowledgeItems",
                column: "CurrentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_TopicId_Status",
                table: "KnowledgeItems",
                columns: new[] { "TopicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_UserId_Scope_Status",
                table: "KnowledgeItems",
                columns: new[] { "UserId", "Scope", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_KnowledgeItem_TopicScope",
                table: "KnowledgeItems",
                sql: "[Scope] = 0 OR ([Scope] = 1 AND [TopicId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_AuthorUserId",
                table: "KnowledgeItemComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_KnowledgeItemRevisionId_CreatedAt",
                table: "KnowledgeItemComments",
                columns: new[] { "KnowledgeItemRevisionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemRevisions_CreatedByUserId",
                table: "KnowledgeItemRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemRevisions_KnowledgeItemId_RevisionNumber",
                table: "KnowledgeItemRevisions",
                columns: new[] { "KnowledgeItemId", "RevisionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItems_KnowledgeItemRevisions_CurrentRevisionId",
                table: "KnowledgeItems",
                column: "CurrentRevisionId",
                principalTable: "KnowledgeItemRevisions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItems_ProjectTopics_TopicId",
                table: "KnowledgeItems",
                column: "TopicId",
                principalTable: "ProjectTopics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItems_Users_UserId",
                table: "KnowledgeItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "KnowledgeItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_KnowledgeItemRevisions_CurrentRevisionId",
                table: "KnowledgeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_ProjectTopics_TopicId",
                table: "KnowledgeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_Users_UserId",
                table: "KnowledgeItems");

            migrationBuilder.DropTable(
                name: "KnowledgeItemComments");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "KnowledgeItems",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "KnowledgeItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "KnowledgeItems",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "KnowledgeItems",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE ki
                SET ki.Title = r.Title,
                    ki.Summary = r.Summary,
                    ki.Content = r.Content,
                    ki.SourceUrl = r.SourceUrl
                FROM KnowledgeItems ki
                INNER JOIN KnowledgeItemRevisions r ON r.KnowledgeItemId = ki.Id AND r.RevisionNumber = 1;
                """);

            migrationBuilder.DropTable(
                name: "KnowledgeItemRevisions");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_CurrentRevisionId",
                table: "KnowledgeItems");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_TopicId_Status",
                table: "KnowledgeItems");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItems_UserId_Scope_Status",
                table: "KnowledgeItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_KnowledgeItem_TopicScope",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "Nickname",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionId",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionNumber",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "KnowledgeItems");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "KnowledgeItems");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_UserId_CategoryId",
                table: "KnowledgeItems",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_UserId_Status",
                table: "KnowledgeItems",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItems_Users_UserId",
                table: "KnowledgeItems",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
