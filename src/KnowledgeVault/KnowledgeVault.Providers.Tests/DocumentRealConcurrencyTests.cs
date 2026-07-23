using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Real optimistic-concurrency proof for KnowledgeItem. Unlike the application-level
// revision guard in DocumentConcurrencyTests (which throws ConflictException before
// any database write), these tests exercise the database-level RowVersion token:
// two independent DbContexts load the same row, then race to update it.
//
// Both contexts share one open in-memory SQLite connection so they see the same
// store. A shared single connection cannot be used concurrently from two threads,
// so the race is modelled as two snapshots: the first commit bumps RowVersion, and
// the second commit (still holding the original/stale value) matches 0 rows and
// raises DbUpdateConcurrencyException, which the API maps to 409.
public sealed class DocumentRealConcurrencyTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<KnowledgeVaultDbContext> _options;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _docId = Guid.NewGuid();

    public DocumentRealConcurrencyTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<KnowledgeVaultDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new KnowledgeVaultDbContext(_options);
        ctx.Database.EnsureCreated();
        ctx.Users.Add(Seed.User(_userId, "owner"));
        ctx.KnowledgeItems.Add(Seed.Document(_docId, _userId, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Draft));
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Second_context_on_stale_snapshot_raises_DbUpdateConcurrencyException()
    {
        await using var ctxA = new KnowledgeVaultDbContext(_options);
        await using var ctxB = new KnowledgeVaultDbContext(_options);

        var itemA = await ctxA.KnowledgeItems.SingleAsync(i => i.Id == _docId);
        var itemB = await ctxB.KnowledgeItems.SingleAsync(i => i.Id == _docId);

        // Both contexts hold the same original RowVersion. Two independent writes
        // target the same row, exactly like two HTTP requests editing one document.
        itemA.Status = KnowledgeItemStatus.Archived;
        itemB.Status = KnowledgeItemStatus.Active;

        // First commit wins and bumps RowVersion in the shared store.
        await ctxA.SaveChangesAsync();

        // Second commit is based on the now-stale snapshot and must fail with a
        // database-level concurrency conflict (mapped to 409 by the API middleware).
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());
    }

    [Fact]
    public async Task Sequential_writers_reloading_succeed_without_false_conflict()
    {
        await using (var ctx1 = new KnowledgeVaultDbContext(_options))
        {
            var item = await ctx1.KnowledgeItems.SingleAsync(i => i.Id == _docId);
            item.Status = KnowledgeItemStatus.Archived;
            await ctx1.SaveChangesAsync();
        }

        await using (var ctx2 = new KnowledgeVaultDbContext(_options))
        {
            var item = await ctx2.KnowledgeItems.SingleAsync(i => i.Id == _docId);
            Assert.Equal(KnowledgeItemStatus.Archived, item.Status);
            item.Status = KnowledgeItemStatus.Active;
            await ctx2.SaveChangesAsync();
        }

        await using (var ctx3 = new KnowledgeVaultDbContext(_options))
        {
            var item = await ctx3.KnowledgeItems.SingleAsync(i => i.Id == _docId);
            Assert.Equal(KnowledgeItemStatus.Active, item.Status);
        }
    }
}
