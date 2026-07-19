using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "KnowledgeItemComments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ResolvedAt",
                table: "KnowledgeItemComments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "KnowledgeItemComments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentRevisionReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeItemRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DecisionComment = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ReviewedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRevisionReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRevisionReviews_KnowledgeItemRevisions_KnowledgeItemRevisionId",
                        column: x => x.KnowledgeItemRevisionId,
                        principalTable: "KnowledgeItemRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentRevisionReviews_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DocumentRevisionReviews_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_ParentCommentId",
                table: "KnowledgeItemComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_ResolvedByUserId",
                table: "KnowledgeItemComments",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisionReviews_KnowledgeItemRevisionId_ReviewerUserId",
                table: "DocumentRevisionReviews",
                columns: new[] { "KnowledgeItemRevisionId", "ReviewerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisionReviews_RequestedByUserId_Status_CreatedAt",
                table: "DocumentRevisionReviews",
                columns: new[] { "RequestedByUserId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRevisionReviews_ReviewerUserId_Status_CreatedAt",
                table: "DocumentRevisionReviews",
                columns: new[] { "ReviewerUserId", "Status", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItemComments_KnowledgeItemComments_ParentCommentId",
                table: "KnowledgeItemComments",
                column: "ParentCommentId",
                principalTable: "KnowledgeItemComments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItemComments_Users_ResolvedByUserId",
                table: "KnowledgeItemComments",
                column: "ResolvedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItemComments_KnowledgeItemComments_ParentCommentId",
                table: "KnowledgeItemComments");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItemComments_Users_ResolvedByUserId",
                table: "KnowledgeItemComments");

            migrationBuilder.DropTable(
                name: "DocumentRevisionReviews");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItemComments_ParentCommentId",
                table: "KnowledgeItemComments");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeItemComments_ResolvedByUserId",
                table: "KnowledgeItemComments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "KnowledgeItemComments");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "KnowledgeItemComments");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "KnowledgeItemComments");
        }
    }
}
