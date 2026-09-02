using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Text;
using KnowledgeVault.Providers;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class DocumentMcpFragmentTests : IAsyncLifetime
{
    private readonly KnowledgeVault.DataAccess.KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _userId = Guid.NewGuid();

    private DocumentProvider Docs() => TestProviders.Documents(_db, _user, _clock);

    public DocumentMcpFragmentTests()
    {
        _user.UserId = _userId;
        _db.Users.Add(Seed.User(_userId, "owner"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Mcp_head_does_not_include_body()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Secret", "# Keep out\n\nprivate body", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        var head = await Docs().GetMcpHeadAsync(created.Id, null, CancellationToken.None);

        Assert.Equal(created.Id, head.Id);
        Assert.Contains(head.Outline, x => x.Heading == "Keep out");
        Assert.DoesNotContain("private body", head.Title);
        Assert.True(head.ContentLength > 0);
        Assert.Equal(DocumentContentHash.Sha256Hex(created.Content), head.ContentHash);
    }

    [Fact]
    public async Task Write_ack_omits_body_and_update_can_omit_content()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Old", "unchanged-body", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        var updated = await Docs().UpdateAsync(created.Id,
            new UpdateDocumentRequest(1, null, null, "New", null, null, null, null, null, "title only",
                null, KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        Assert.Equal(2, updated.CurrentRevisionNumber);
        Assert.Equal("New", updated.Title);
        Assert.Equal("unchanged-body", updated.Content);

        var ack = await Docs().GetWriteAckAsync(created.Id, CancellationToken.None);
        Assert.Equal("New", ack.Title);
        Assert.Equal("unchanged-body".Length, ack.ContentLength);
        Assert.DoesNotContain("unchanged-body", ack.Title);
    }

    [Fact]
    public async Task Apply_patch_creates_one_revision_for_two_hunks()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Doc", "# A\n\nfoo\n\n# B\n\nbar\n", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        var ack = await Docs().ApplyPatchAsync(created.Id,
            new ApplyDocumentPatchRequest(1,
            [
                new DocumentPatchHunk("foo", "FOO"),
                new DocumentPatchHunk("bar", "BAR")
            ],
            "two edits"),
            CancellationToken.None);

        Assert.Equal(2, ack.CurrentRevisionNumber);
        Assert.Equal(2, ack.AppliedCount);
        var current = await Docs().GetAsync(created.Id, CancellationToken.None);
        Assert.Contains("FOO", current.Content);
        Assert.Contains("BAR", current.Content);
        Assert.DoesNotContain("foo", current.Content);
    }

    [Fact]
    public async Task Apply_patch_rejects_stale_revision()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Doc", "alpha", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Docs().ApplyPatchAsync(created.Id,
                new ApplyDocumentPatchRequest(99, [new DocumentPatchHunk("alpha", "beta")], null),
                CancellationToken.None));
        Assert.Contains("Current revision is 1", ex.Message);
    }

    [Fact]
    public async Task Metadata_update_does_not_create_a_revision()
    {
        var created = await Docs().CreateAsync(
            new CreateDocumentRequest(DocumentScope.Personal, null, null, DocumentType.General,
                "Doc", "body-stays", null, null, null, null, null, null,
                KnowledgeItemStatus.Draft, null, null),
            CancellationToken.None);

        await Docs().UpdateMetadataAsync(created.Id,
            new UpdateDocumentMetadataRequest(null, null, null, KnowledgeItemStatus.Active, null, null),
            CancellationToken.None);

        var current = await Docs().GetAsync(created.Id, CancellationToken.None);
        Assert.Equal(1, current.CurrentRevisionNumber);
        Assert.Equal(KnowledgeItemStatus.Active, current.Status);
        Assert.Equal("body-stays", current.Content);
    }
}
