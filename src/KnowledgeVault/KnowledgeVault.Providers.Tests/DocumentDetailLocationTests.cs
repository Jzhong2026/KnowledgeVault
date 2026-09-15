using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Enums;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class DocumentDetailLocationTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _userId = Guid.NewGuid();

    public DocumentDetailLocationTests()
    {
        _user.UserId = _userId;
        _db.Users.Add(Seed.User(_userId, "owner"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    [Fact]
    public async Task Get_includes_project_name_and_folder_path_from_root_to_document_folder()
    {
        var projectId = Guid.NewGuid();
        var guidesId = Guid.NewGuid();
        var voiceId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        _db.Projects.Add(Seed.Project(projectId, "Atlas", _userId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Owner));
        _db.Folders.Add(Seed.Folder(guidesId, "Guides", DocumentScope.Project, null, projectId));
        _db.Folders.Add(Seed.Folder(voiceId, "Voice", DocumentScope.Project, null, projectId, guidesId));

        var document = Seed.Document(documentId, _userId, DocumentScope.Project, projectId, 1, KnowledgeItemStatus.Active);
        document.FolderId = voiceId;
        _db.KnowledgeItems.Add(document);
        _db.KnowledgeItemRevisions.Add(Seed.Revision(Guid.NewGuid(), documentId, 1, _userId));
        await _db.SaveChangesAsync();

        var dto = await TestProviders.Documents(_db, _user, _clock)
            .GetAsync(documentId, CancellationToken.None);

        Assert.Equal(voiceId, dto.FolderId);
        Assert.Equal("Atlas", dto.ProjectName);
        Assert.Equal(2, dto.FolderPath.Count);
        Assert.Equal(guidesId, dto.FolderPath[0].Id);
        Assert.Equal("Guides", dto.FolderPath[0].Name);
        Assert.Equal(voiceId, dto.FolderPath[1].Id);
        Assert.Equal("Voice", dto.FolderPath[1].Name);
    }

    [Fact]
    public async Task Get_returns_empty_folder_path_for_root_documents()
    {
        var projectId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "Atlas", _userId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Owner));
        _db.KnowledgeItems.Add(Seed.Document(documentId, _userId, DocumentScope.Project, projectId, 1, KnowledgeItemStatus.Active));
        _db.KnowledgeItemRevisions.Add(Seed.Revision(Guid.NewGuid(), documentId, 1, _userId));
        await _db.SaveChangesAsync();

        var dto = await TestProviders.Documents(_db, _user, _clock)
            .GetAsync(documentId, CancellationToken.None);

        Assert.Null(dto.FolderId);
        Assert.Equal("Atlas", dto.ProjectName);
        Assert.Empty(dto.FolderPath);
    }
}
