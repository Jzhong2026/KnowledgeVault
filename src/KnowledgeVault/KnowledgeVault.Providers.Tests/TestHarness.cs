using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Providers;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.Providers.Tests;

// Shared fakes + in-memory SQLite factory used by the change-impact test suite.
// Mirrors the existing Document*ProviderTests harness so the new tests run against
// a real (in-memory) database without mocking the persistence layer.

public sealed class FakeCurrentUser : ICurrentUserContext
{
    public bool IsAuthenticated => UserId != Guid.Empty;
    public Guid UserId { get; set; }
}

public sealed class FakeClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

internal sealed class NoopProjectMemoryProvider : IProjectMemoryProvider
{
    public Task<KnowledgeItemDto> GetAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult<KnowledgeItemDto>(null!);

    public Task<Guid> EnsureExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        Task.FromResult(projectId);

    public Task<int> EnsureAllExistAsync(CancellationToken cancellationToken) => Task.FromResult(0);
}

internal static class TestDb
{
    public static KnowledgeVaultDbContext Create()
    {
        var options = new DbContextOptionsBuilder<KnowledgeVaultDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var ctx = new KnowledgeVaultDbContext(options);
        ctx.Database.OpenConnection();
        ctx.Database.EnsureCreated();
        return ctx;
    }
}

internal static class Seed
{
    private static readonly DateTimeOffset Fixed = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static User User(Guid id, string name) => new()
    {
        Id = id,
        UserName = name,
        NormalizedUserName = name.ToUpperInvariant(),
        Email = $"{name}@example.test",
        NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
        PasswordHash = "hash",
        PasswordSalt = "salt",
        CreatedAt = Fixed,
    };

    public static Project Project(Guid id, string name, Guid ownerId) => new()
    {
        Id = id,
        Name = name,
        OwnerUserId = ownerId,
        IsArchived = false,
        CreatedAt = Fixed,
    };

    public static ProjectMember Member(Guid projectId, Guid userId, ProjectRole role) => new()
    {
        ProjectId = projectId,
        UserId = userId,
        Role = role,
        CreatedAt = Fixed,
    };

    public static Folder Folder(
        Guid id, string name, DocumentScope scope, Guid? ownerUserId, Guid? projectId, Guid? parentFolderId = null) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        Scope = scope,
        OwnerUserId = ownerUserId,
        ProjectId = projectId,
        ParentFolderId = parentFolderId,
        CreatedAt = Fixed,
    };

    public static KnowledgeItem Document(
        Guid id, Guid ownerId, DocumentScope scope, Guid? projectId, int revision,
        KnowledgeItemStatus status = KnowledgeItemStatus.Draft) => new()
    {
        Id = id,
        OwnerUserId = ownerId,
        Scope = scope,
        ProjectId = projectId,
        DocumentType = DocumentType.General,
        Status = status,
        CurrentRevisionNumber = revision,
        CreatedAt = Fixed,
    };

    public static KnowledgeItemRevision Revision(Guid id, Guid itemId, int number, Guid by) => new()
    {
        Id = id,
        KnowledgeItemId = itemId,
        RevisionNumber = number,
        Title = $"Revision {number}",
        Content = "# Content",
        CreatedByUserId = by,
        CreatedAt = Fixed,
    };
}

// Builds the refactored providers with all their collaborators so tests do not
// have to repeat the (now larger) constructor argument lists. Mirrors the
// composition done by the production DI container.
internal static class TestProviders
{
    public static ProjectAccessService ProjectAccess(KnowledgeVaultDbContext db) => new(db);

    public static DocumentAccessService DocAccess(
        KnowledgeVaultDbContext db, ICurrentUserContext user, ProjectAccessService? projectAccess = null)
        => new(db, user, projectAccess ?? ProjectAccess(db));

    public static DocumentTagService TagService(KnowledgeVaultDbContext db, IDateTimeProvider clock)
        => new(db, clock);

    public static DocumentLocationService LocationService(KnowledgeVaultDbContext db, ProjectAccessService access)
        => new(db, access);

    public static DocumentProvider Documents(KnowledgeVaultDbContext db, ICurrentUserContext user, IDateTimeProvider clock)
    {
        var projectAccess = ProjectAccess(db);
        return new DocumentProvider(
            db,
            user,
            clock,
            DocAccess(db, user, projectAccess),
            projectAccess,
            TagService(db, clock),
            LocationService(db, projectAccess));
    }

    public static FolderProvider Folders(KnowledgeVaultDbContext db, ICurrentUserContext user, IDateTimeProvider clock)
        => new(db, user, clock, ProjectAccess(db));
}
