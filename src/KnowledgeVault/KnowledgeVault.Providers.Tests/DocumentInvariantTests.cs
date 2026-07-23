using System;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Guards the status-transition invariants that currently live in
// DocumentProvider.ApplyStatusTimestamps. When these invariants are moved into the
// domain (KnowledgeItem), this suite must stay green so no timestamp rule regresses.
public sealed class DocumentInvariantTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _userId = Guid.NewGuid();

    private DocumentProvider Docs() => TestProviders.Documents(_db, _user, _clock);

    public DocumentInvariantTests()
    {
        _user.UserId = _userId;
        _db.Users.Add(Seed.User(_userId, "owner"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private async Task<Guid> SeedPersonalDoc(KnowledgeItemStatus status)
    {
        var id = Guid.NewGuid();
        _db.KnowledgeItems.Add(Seed.Document(id, _userId, DocumentScope.Personal, null, 1, status));
        _db.KnowledgeItemRevisions.Add(Seed.Revision(Guid.NewGuid(), id, 1, _userId));
        await _db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Creating_active_document_sets_published_at_and_clears_archived_at()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Title", "Content", null, null, null, null, null, null, KnowledgeItemStatus.Active, null, null),
            CancellationToken.None);

        var stored = await _db.KnowledgeItems.SingleAsync(d => d.Id == created.Id);
        Assert.NotNull(stored.PublishedAt);
        Assert.Null(stored.ArchivedAt);
    }

    [Fact]
    public async Task Creating_draft_document_leaves_timestamps_unset()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Title", "Content", null, null, null, null, null, null, KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        var stored = await _db.KnowledgeItems.SingleAsync(d => d.Id == created.Id);
        Assert.Null(stored.PublishedAt);
        Assert.Null(stored.ArchivedAt);
    }

    [Fact]
    public async Task Archiving_document_sets_archived_at()
    {
        var id = await SeedPersonalDoc(KnowledgeItemStatus.Active);

        await Docs().UpdateMetadataAsync(id,
            new UpdateDocumentMetadataRequest(null, null, null, KnowledgeItemStatus.Archived, null, null),
            CancellationToken.None);

        var stored = await _db.KnowledgeItems.SingleAsync(d => d.Id == id);
        Assert.NotNull(stored.ArchivedAt);
    }
}
