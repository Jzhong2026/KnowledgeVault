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
    public async Task Non_empty_folder_is_recursively_archived()
    {
        var folderId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(folderId, "F", DocumentScope.Personal, _userId, null));
        var nonEmptyDoc = Seed.Document(Guid.NewGuid(), _userId, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Active);
        nonEmptyDoc.FolderId = folderId;
        _db.KnowledgeItems.Add(nonEmptyDoc);
        await _db.SaveChangesAsync();

        await Folders().ArchiveAsync(folderId, CancellationToken.None);

        Assert.True((await _db.Folders.SingleAsync()).IsArchived);
        Assert.Equal(KnowledgeItemStatus.Archived, (await _db.KnowledgeItems.SingleAsync()).Status);
    }

    [Fact]
    public async Task Empty_folder_can_be_deleted()
    {
        var folderId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(folderId, "F", DocumentScope.Personal, _userId, null));
        await _db.SaveChangesAsync();

        await Folders().DeleteAsync(folderId, CancellationToken.None);

        Assert.True((await _db.Folders.SingleAsync()).IsArchived);
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

    [Fact]
    public async Task Paged_project_root_filters_documents_by_creator()
    {
        var projectId = Guid.NewGuid();
        var myDocumentId = Guid.NewGuid();
        var otherDocumentId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _otherId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Editor));
        _db.KnowledgeItems.AddRange(
            Seed.Document(myDocumentId, _userId, DocumentScope.Project, projectId, 1),
            Seed.Document(otherDocumentId, _otherId, DocumentScope.Project, projectId, 1));
        await _db.SaveChangesAsync();

        var content = await Folders().GetContentPagedAsync(
            DocumentScope.Project, projectId, null, null, false, null, _otherId, 1, 20, CancellationToken.None);

        var document = Assert.Single(content.Documents);
        Assert.Equal(otherDocumentId, document.Id);
        Assert.Equal("other", document.OwnerDisplayName);
        Assert.Equal(1, content.TotalDocumentCount);
    }

    [Fact]
    public async Task Paged_root_name_filter_matches_folder_names_and_document_titles()
    {
        var matchingFolderId = Guid.NewGuid();
        _db.Folders.AddRange(
            Seed.Folder(matchingFolderId, "Match folder", DocumentScope.Personal, _userId, null),
            Seed.Folder(Guid.NewGuid(), "Other folder", DocumentScope.Personal, _userId, null));

        var matchingDocument = Seed.Document(Guid.NewGuid(), _userId, DocumentScope.Personal, null, 1);
        var otherDocument = Seed.Document(Guid.NewGuid(), _userId, DocumentScope.Personal, null, 1);
        _db.KnowledgeItems.AddRange(matchingDocument, otherDocument);
        await _db.SaveChangesAsync();

        var matchingRevision = Seed.Revision(Guid.NewGuid(), matchingDocument.Id, 1, _userId);
        matchingRevision.Title = "Match document";
        var otherRevision = Seed.Revision(Guid.NewGuid(), otherDocument.Id, 1, _userId);
        otherRevision.Title = "Other document";
        _db.KnowledgeItemRevisions.AddRange(matchingRevision, otherRevision);
        await _db.SaveChangesAsync();

        matchingDocument.CurrentRevisionId = matchingRevision.Id;
        otherDocument.CurrentRevisionId = otherRevision.Id;
        await _db.SaveChangesAsync();

        var content = await Folders().GetContentPagedAsync(
            DocumentScope.Personal, null, null, null, false, "Match", null, 1, 20, CancellationToken.None);

        Assert.Equal(matchingFolderId, Assert.Single(content.Folders).Id);
        Assert.Equal(matchingDocument.Id, Assert.Single(content.Documents).Id);
        Assert.Equal(1, content.TotalFolderCount);
        Assert.Equal(1, content.TotalDocumentCount);
    }

    [Fact]
    public async Task Tree_rooted_at_root_includes_direct_child_in_children()
    {
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(rootId, "Root", DocumentScope.Personal, _userId, null, null));
        _db.Folders.Add(Seed.Folder(childId, "Child", DocumentScope.Personal, _userId, null, rootId));
        await _db.SaveChangesAsync();

        var tree = await Folders().GetTreeAsync(DocumentScope.Personal, null, rootId, CancellationToken.None);

        Assert.Equal(rootId, tree.Id);
        Assert.Contains(tree.Children, c => c.Id == childId);
    }

    [Fact]
    public async Task Tree_builds_multi_level_hierarchy_under_root()
    {
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var grandId = Guid.NewGuid();
        _db.Folders.Add(Seed.Folder(rootId, "Root", DocumentScope.Personal, _userId, null, null));
        _db.Folders.Add(Seed.Folder(childId, "Child", DocumentScope.Personal, _userId, null, rootId));
        _db.Folders.Add(Seed.Folder(grandId, "Grand", DocumentScope.Personal, _userId, null, childId));
        await _db.SaveChangesAsync();

        var tree = await Folders().GetTreeAsync(DocumentScope.Personal, null, rootId, CancellationToken.None);

        Assert.Equal(rootId, tree.Id);
        var childNode = Assert.Single(tree.Children);
        Assert.Equal(childId, childNode.Id);
        var grandNode = Assert.Single(childNode.Children);
        Assert.Equal(grandId, grandNode.Id);
    }

    [Fact]
    public async Task Archive_and_restore_folder_recursively_updates_descendants_and_documents()
    {
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _db.Folders.AddRange(
            Seed.Folder(rootId, "Root", DocumentScope.Personal, _userId, null),
            Seed.Folder(childId, "Child", DocumentScope.Personal, _userId, null, rootId));
        var document = Seed.Document(documentId, _userId, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Active);
        document.FolderId = childId;
        _db.KnowledgeItems.Add(document);
        await _db.SaveChangesAsync();

        await Folders().ArchiveAsync(rootId, CancellationToken.None);

        Assert.All(await _db.Folders.ToListAsync(), folder => Assert.True(folder.IsArchived));
        Assert.Equal(KnowledgeItemStatus.Archived, (await _db.KnowledgeItems.SingleAsync()).Status);

        await Folders().RestoreAsync(rootId, CancellationToken.None);

        Assert.All(await _db.Folders.ToListAsync(), folder => Assert.False(folder.IsArchived));
        var restored = await _db.KnowledgeItems.SingleAsync();
        Assert.Equal(KnowledgeItemStatus.Active, restored.Status);
        Assert.Null(restored.ArchivedAt);
    }

    [Fact]
    public async Task Mcp_descendant_listing_is_flat_and_ignores_user_visibility()
    {
        var rootId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        _db.Folders.AddRange(
            Seed.Folder(rootId, "Root", DocumentScope.Personal, _otherId, null),
            Seed.Folder(childId, "Child", DocumentScope.Personal, _otherId, null, rootId));
        var document = Seed.Document(documentId, _otherId, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Active);
        var revision = Seed.Revision(Guid.NewGuid(), documentId, 1, _otherId);
        document.FolderId = rootId;
        _db.KnowledgeItems.Add(document);
        await _db.SaveChangesAsync();
        _db.KnowledgeItemRevisions.Add(revision);
        await _db.SaveChangesAsync();
        document.CurrentRevisionId = revision.Id;
        await _db.SaveChangesAsync();

        var items = await Folders().ListDescendantsForMcpAsync(rootId, CancellationToken.None);

        Assert.Contains(items, item => item.Id == childId && item.Type == "folder" && item.Path == "Child");
        Assert.Contains(items, item => item.Id == documentId && item.Type == "document" && item.Path == "Revision 1");
    }
}
