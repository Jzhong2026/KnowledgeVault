using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Providers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Safety net for FolderProvider, which is a refactor target (currently 506 lines,
// duplicated access queries). These characterize current behavior so the upcoming
// split / domain-move refactor cannot silently change folder semantics.
public sealed class FolderProviderTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();

    public FolderProviderTests()
    {
        _user.UserId = _userId;
        _db.Users.AddRange(Seed.User(_userId, "owner"), Seed.User(_otherId, "other"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private FolderProvider Folders() => TestProviders.Folders(_db, _user, _clock);

    [Fact]
    public async Task Personal_folder_create_assigns_owner_and_scope()
    {
        var created = await Folders().CreateAsync(
            new CreateFolderRequest(DocumentScope.Personal, null, null, "Notes", null), CancellationToken.None);

        Assert.Equal(DocumentScope.Personal, created.Scope);
        var stored = await _db.Folders.SingleAsync(f => f.Id == created.Id);
        Assert.Equal(_userId, stored.OwnerUserId);
    }

    [Fact]
    public async Task Personal_folder_cannot_have_project_id()
    {
        var projectId = Guid.NewGuid();
        await Assert.ThrowsAsync<ValidationException>(() =>
            Folders().CreateAsync(new CreateFolderRequest(DocumentScope.Personal, projectId, null, "X", null), CancellationToken.None));
    }

    [Fact]
    public async Task Project_folder_requires_editor_role()
    {
        var projectId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _otherId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Viewer));
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Folders().CreateAsync(new CreateFolderRequest(DocumentScope.Project, projectId, null, "X", null), CancellationToken.None));
    }

    [Fact]
    public async Task Project_folder_created_by_editor_succeeds()
    {
        var projectId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _otherId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Editor));
        await _db.SaveChangesAsync();

        var created = await Folders().CreateAsync(
            new CreateFolderRequest(DocumentScope.Project, projectId, null, "Team", null), CancellationToken.None);

        Assert.Equal(projectId, created.ProjectId);
        Assert.Equal(DocumentScope.Project, created.Scope);
        var stored = await _db.Folders.SingleAsync(f => f.Id == created.Id);
        Assert.Equal(projectId, stored.ProjectId);
    }

    [Fact]
    public async Task Non_empty_folder_cannot_be_deleted()
    {
        var folderId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(folderId, "F", DocumentScope.Personal, _userId, null));
        var nonEmptyDoc = Seed.Document(Guid.NewGuid(), _userId, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Active);
        nonEmptyDoc.FolderId = folderId;
        _db.KnowledgeItems.Add(nonEmptyDoc);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            Folders().DeleteAsync(folderId, CancellationToken.None));
    }

    [Fact]
    public async Task Accessible_folder_query_returns_only_own_personal_folders()
    {
        var myFolder = Guid.NewGuid();
        var othersFolder = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(myFolder, "Mine", DocumentScope.Personal, _userId, null));
        _db.Folders.Add(Seed.Folder(othersFolder, "Theirs", DocumentScope.Personal, _otherId, null));
        await _db.SaveChangesAsync();

        var content = await Folders().GetContentAsync(DocumentScope.Personal, null, null, null, CancellationToken.None);

        Assert.Single(content.Folders);
        Assert.Equal(myFolder, content.Folders[0].Id);
    }
}
