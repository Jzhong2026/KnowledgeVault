using System;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Providers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Safety net for the document write paths that the concurrency + provider-split
// work will touch: revision conflict detection, revision advancement, and move.
public sealed class DocumentConcurrencyTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _userId = Guid.NewGuid();

    private DocumentProvider Docs() => TestProviders.Documents(_db, _user, _clock);

    public DocumentConcurrencyTests()
    {
        _user.UserId = _userId;
        _db.Users.Add(Seed.User(_userId, "owner"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private async Task<Guid> SeedPersonalDoc(KnowledgeItemStatus status = KnowledgeItemStatus.Draft)
    {
        var id = Guid.NewGuid();
        _db.KnowledgeItems.Add(Seed.Document(id, _userId, DocumentScope.Personal, null, 1, status));
        _db.KnowledgeItemRevisions.Add(Seed.Revision(Guid.NewGuid(), id, 1, _userId));
        await _db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Stale_expected_revision_number_is_rejected_with_conflict()
    {
        var id = await SeedPersonalDoc();

        await Assert.ThrowsAsync<ConflictException>(() =>
            Docs().UpdateAsync(id,
                new UpdateDocumentRequest(99, null, null, "T", "C", null, null, null, null, null, null,
                    KnowledgeItemStatus.Draft, null, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Update_advances_revision_number_and_keeps_metadata()
    {
        var id = await SeedPersonalDoc();

        var updated = await Docs().UpdateAsync(id,
            new UpdateDocumentRequest(1, null, null, "NewTitle", "NewContent", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        Assert.Equal(2, updated.CurrentRevisionNumber);
        var revisionCount = await _db.KnowledgeItemRevisions.CountAsync(r => r.KnowledgeItemId == id);
        Assert.Equal(2, revisionCount);
    }

    [Fact]
    public async Task Move_document_updates_folder()
    {
        var id = await SeedPersonalDoc();
        var folderId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(folderId, "F", DocumentScope.Personal, _userId, null));
        await _db.SaveChangesAsync();

        await Docs().MoveDocumentAsync(id, folderId, CancellationToken.None);

        var doc = await _db.KnowledgeItems.SingleAsync(d => d.Id == id);
        Assert.Equal(folderId, doc.FolderId);
    }
}
