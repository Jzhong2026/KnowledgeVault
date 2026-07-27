using KnowledgeVault.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeVault.DataAccess.Migrations;

/// <summary>
/// Repairs the server data so every existing account can access the shared
/// AI-Research project. Owners retain the Owner role; every other account is
/// added as an Editor. The unique ProjectMembers index makes the migration
/// safe for users who were already members.
/// </summary>
[DbContext(typeof(KnowledgeVaultDbContext))]
[Migration("20260728100000_AssignAllUsersToDefaultProject")]
public partial class AssignAllUsersToDefaultProject : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO "ProjectMembers" ("Id", "ProjectId", "UserId", "Role", "CreatedAt", "UpdatedAt")
            SELECT
                lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-' ||
                lower(hex(randomblob(2))) || '-' || lower(hex(randomblob(2))) || '-' ||
                lower(hex(randomblob(6))),
                project."Id",
                user."Id",
                CASE WHEN user."Id" = project."OwnerUserId" THEN 0 ELSE 1 END,
                CAST((julianday('now') - 2440587.5) * 86400000 AS INTEGER),
                NULL
            FROM "Users" AS user
            CROSS JOIN "Projects" AS project
            WHERE project."Name" = 'AI-Research'
              AND project."IsArchived" = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM "ProjectMembers" AS member
                  WHERE member."ProjectId" = project."Id"
                    AND member."UserId" = user."Id"
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Membership is user data. Do not remove it if a deployment is rolled back.
    }
}
