using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KnowledgeVault.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqliteSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystem = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    LastLoginAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTopics_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<long>(type: "INTEGER", nullable: true),
                    LastUsedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    RevokedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKeys_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProjectMemoryCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetSection = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedContent = table.Column<string>(type: "TEXT", nullable: false),
                    Rationale = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemoryRevisionAtProposal = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    AppliedMemoryRevisionNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMemoryCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMemoryCandidates_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectMemoryCandidates_Users_ProposedByUserId",
                        column: x => x.ProposedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProjectMemoryCandidates_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

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

            migrationBuilder.CreateTable(
                name: "KnowledgeItemComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeItemRevisionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    DeletedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ResolvedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemComments_KnowledgeItemComments_ParentCommentId",
                        column: x => x.ParentCommentId,
                        principalTable: "KnowledgeItemComments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItemComments_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItemComments_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeItemRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    LinkDisplayText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LinkUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    ChangeNote = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItemRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemRevisions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TopicId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DocumentType = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CurrentRevisionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PublishedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    ArchivedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItems", x => x.Id);
                    table.CheckConstraint("CK_KnowledgeItem_TopicScope", "(\"Scope\" = 0 AND \"ProjectId\" IS NULL AND \"TopicId\" IS NULL) OR (\"Scope\" = 1 AND \"ProjectId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_KnowledgeItems_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItems_KnowledgeItemRevisions_CurrentRevisionId",
                        column: x => x.CurrentRevisionId,
                        principalTable: "KnowledgeItemRevisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItems_ProjectTopics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "ProjectTopics",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_KnowledgeItems_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeItemTags",
                columns: table => new
                {
                    KnowledgeItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeItemTags", x => new { x.KnowledgeItemId, x.TagId });
                    table.ForeignKey(
                        name: "FK_KnowledgeItemTags_KnowledgeItems_KnowledgeItemId",
                        column: x => x.KnowledgeItemId,
                        principalTable: "KnowledgeItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KnowledgeItemTags_Tags_TagId",
                        column: x => x.TagId,
                        principalTable: "Tags",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "Id", "Color", "CreatedAt", "Description", "IsArchived", "IsSystem", "Name", "NormalizedName", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "#0f766e", 1784455654355L, "Shared durable context maintained through project review.", false, true, "Memory", "MEMORY", -300, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "#2563eb", 1784455654355L, "Task definitions, execution notes, and delivery tracking.", false, true, "Task", "TASK", -200, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "#7c3aed", 1784455654355L, "Architecture, product, and implementation design documents.", false, true, "Design", "DESIGN", -100, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_Prefix",
                table: "ApiKeys",
                column: "Prefix");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_UserId_RevokedAt",
                table: "ApiKeys",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_NormalizedName",
                table: "Categories",
                column: "NormalizedName",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_AuthorUserId",
                table: "KnowledgeItemComments",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_KnowledgeItemRevisionId_CreatedAt",
                table: "KnowledgeItemComments",
                columns: new[] { "KnowledgeItemRevisionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_ParentCommentId",
                table: "KnowledgeItemComments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemComments_ResolvedByUserId",
                table: "KnowledgeItemComments",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemRevisions_CreatedByUserId",
                table: "KnowledgeItemRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemRevisions_KnowledgeItemId_RevisionNumber",
                table: "KnowledgeItemRevisions",
                columns: new[] { "KnowledgeItemId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_CategoryId",
                table: "KnowledgeItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_CurrentRevisionId",
                table: "KnowledgeItems",
                column: "CurrentRevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_ProjectId_ProjectMemory",
                table: "KnowledgeItems",
                column: "ProjectId",
                unique: true,
                filter: "\"DocumentType\" = 3");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_ProjectId_Status",
                table: "KnowledgeItems",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_TopicId_Status",
                table: "KnowledgeItems",
                columns: new[] { "TopicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItems_UserId_Scope_Status",
                table: "KnowledgeItems",
                columns: new[] { "UserId", "Scope", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeItemTags_TagId",
                table: "KnowledgeItemTags",
                column: "TagId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_ProjectId_UserId",
                table: "ProjectMembers",
                columns: new[] { "ProjectId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_UserId",
                table: "ProjectMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemoryCandidates_ProjectId_Status_CreatedAt",
                table: "ProjectMemoryCandidates",
                columns: new[] { "ProjectId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemoryCandidates_ProposedByUserId",
                table: "ProjectMemoryCandidates",
                column: "ProposedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMemoryCandidates_ReviewedByUserId",
                table: "ProjectMemoryCandidates",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerUserId",
                table: "Projects",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTopics_ProjectId_NormalizedName",
                table: "ProjectTopics",
                columns: new[] { "ProjectId", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTopics_ProjectId_SortOrder",
                table: "ProjectTopics",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Tags_NormalizedName",
                table: "Tags",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUserName",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentRevisionReviews_KnowledgeItemRevisions_KnowledgeItemRevisionId",
                table: "DocumentRevisionReviews",
                column: "KnowledgeItemRevisionId",
                principalTable: "KnowledgeItemRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItemComments_KnowledgeItemRevisions_KnowledgeItemRevisionId",
                table: "KnowledgeItemComments",
                column: "KnowledgeItemRevisionId",
                principalTable: "KnowledgeItemRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_KnowledgeItemRevisions_KnowledgeItems_KnowledgeItemId",
                table: "KnowledgeItemRevisions",
                column: "KnowledgeItemId",
                principalTable: "KnowledgeItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItemRevisions_Users_CreatedByUserId",
                table: "KnowledgeItemRevisions");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_Users_UserId",
                table: "KnowledgeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_KnowledgeItems_KnowledgeItemRevisions_CurrentRevisionId",
                table: "KnowledgeItems");

            migrationBuilder.DropTable(
                name: "ApiKeys");

            migrationBuilder.DropTable(
                name: "DocumentRevisionReviews");

            migrationBuilder.DropTable(
                name: "KnowledgeItemComments");

            migrationBuilder.DropTable(
                name: "KnowledgeItemTags");

            migrationBuilder.DropTable(
                name: "ProjectMembers");

            migrationBuilder.DropTable(
                name: "ProjectMemoryCandidates");

            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "KnowledgeItemRevisions");

            migrationBuilder.DropTable(
                name: "KnowledgeItems");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "ProjectTopics");

            migrationBuilder.DropTable(
                name: "Projects");
        }
    }
}
