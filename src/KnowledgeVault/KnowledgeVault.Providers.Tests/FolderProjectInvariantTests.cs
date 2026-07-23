using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Exceptions;
using KnowledgeVault.Infrastructure.Time;
using KnowledgeVault.Providers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Phase B: locks the Folder/Project permission + invariant matrix against regression.
// Every test runs against a real in-memory SQLite database via the shared TestDb/
// Seed/FakeCurrentUser harness (see TestHarness.cs). Permissions are exercised by
// pointing FakeCurrentUser.UserId at different seeded users, not by spinning up a
// second DbContext, so the single in-memory database is shared by provider + reads.

public abstract class FolderProjectInvariantTestBase : IAsyncLifetime
{
    protected KnowledgeVaultDbContext Db = TestDb.Create();
    protected FakeCurrentUser User = new();
    protected FakeClock Clock = new();

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Db.DisposeAsync().AsTask();

    protected async Task<Guid> SeedUser(string name)
    {
        var id = Guid.NewGuid();
        Db.Users.Add(Seed.User(id, name));
        await Db.SaveChangesAsync();
        return id;
    }

    protected async Task<(Guid ProjectId, Guid OwnerId)> SeedProject(string name, Guid ownerId)
    {
        var projectId = Guid.NewGuid();
        Db.Projects.Add(Seed.Project(projectId, name, ownerId));
        Db.ProjectMembers.Add(Seed.Member(projectId, ownerId, ProjectRole.Owner));
        await Db.SaveChangesAsync();
        return (projectId, ownerId);
    }

    protected async Task AddMember(Guid projectId, Guid userId, ProjectRole role)
    {
        Db.ProjectMembers.Add(Seed.Member(projectId, userId, role));
        await Db.SaveChangesAsync();
    }
}

// ---------------------------------------------------------------------------
// Folder hierarchy + scope invariants
// ---------------------------------------------------------------------------
public sealed class FolderHierarchyInvariantTests : FolderProjectInvariantTestBase
{
    [Fact]
    public async Task Personal_folder_cannot_carry_a_project_id()
    {
        User.UserId = await SeedUser("owner");
        var folders = TestProviders.Folders(Db, User, Clock);

        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Personal, Guid.NewGuid(), null, "X", null), default));
    }

    [Fact]
    public async Task Project_folder_requires_a_project_id()
    {
        User.UserId = await SeedUser("owner");
        var folders = TestProviders.Folders(Db, User, Clock);

        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Project, null, null, "X", null), default));
    }

    [Fact]
    public async Task New_folder_parent_must_share_scope_and_project()
    {
        User.UserId = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", User.UserId);
        var personal = Seed.Folder(Guid.NewGuid(), "Personal", DocumentScope.Personal, User.UserId, null);
        Db.Folders.Add(personal);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Project, projectId, personal.Id, "Child", null), default));
    }

    [Fact]
    public async Task Reparenting_must_preserve_scope_and_project()
    {
        User.UserId = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", User.UserId);
        var projectFolder = Seed.Folder(Guid.NewGuid(), "ProjRoot", DocumentScope.Project, null, projectId);
        var personal = Seed.Folder(Guid.NewGuid(), "Personal", DocumentScope.Personal, User.UserId, null);
        Db.Folders.AddRange(projectFolder, personal);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.UpdateAsync(personal.Id, new UpdateFolderRequest(null, null, projectFolder.Id, null), default));
    }

    [Fact]
    public async Task A_folder_cannot_be_its_own_parent()
    {
        User.UserId = await SeedUser("owner");
        var folder = Seed.Folder(Guid.NewGuid(), "F", DocumentScope.Personal, User.UserId, null);
        Db.Folders.Add(folder);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.UpdateAsync(folder.Id, new UpdateFolderRequest(null, null, folder.Id, null), default));
    }

    [Fact]
    public async Task Moving_a_folder_into_its_own_descendant_is_rejected()
    {
        User.UserId = await SeedUser("owner");
        var parent = Seed.Folder(Guid.NewGuid(), "Parent", DocumentScope.Personal, User.UserId, null);
        var child = Seed.Folder(Guid.NewGuid(), "Child", DocumentScope.Personal, User.UserId, null, parent.Id);
        Db.Folders.AddRange(parent, child);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.UpdateAsync(parent.Id, new UpdateFolderRequest(null, null, child.Id, null), default));
    }
}

// ---------------------------------------------------------------------------
// Folder unique-sibling-name invariant
// ---------------------------------------------------------------------------
public sealed class FolderUniqueNameInvariantTests : FolderProjectInvariantTestBase
{
    [Fact]
    public async Task Personal_sibling_with_same_name_is_rejected()
    {
        User.UserId = await SeedUser("owner");
        Db.Folders.Add(Seed.Folder(Guid.NewGuid(), "Notes", DocumentScope.Personal, User.UserId, null));
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Personal, null, null, "notes", null), default));
    }

    [Fact]
    public async Task Project_sibling_with_same_name_is_rejected()
    {
        User.UserId = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", User.UserId);
        Db.Folders.Add(Seed.Folder(Guid.NewGuid(), "Docs", DocumentScope.Project, null, projectId));
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Project, projectId, null, "docs", null), default));
    }

    [Fact]
    public async Task Same_name_allowed_under_a_different_parent()
    {
        User.UserId = await SeedUser("owner");
        var a = Seed.Folder(Guid.NewGuid(), "A", DocumentScope.Personal, User.UserId, null);
        Db.Folders.Add(a);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        var created = await folders.CreateAsync(
            new CreateFolderRequest(DocumentScope.Personal, null, a.Id, "Notes", null), default);
        Assert.Equal("Notes", created.Name);
    }
}

// ---------------------------------------------------------------------------
// Folder create / edit / delete permissions
// ---------------------------------------------------------------------------
public sealed class FolderPermissionTests : FolderProjectInvariantTestBase
{
    [Fact]
    public async Task Non_member_cannot_create_a_project_folder()
    {
        var owner = await SeedUser("owner");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = stranger;
        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Project, projectId, null, "Docs", null), default));
    }

    [Fact]
    public async Task Viewer_cannot_create_a_project_folder()
    {
        var owner = await SeedUser("owner");
        var viewer = await SeedUser("viewer");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, viewer, ProjectRole.Viewer);

        User.UserId = viewer;
        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            folders.CreateAsync(new CreateFolderRequest(DocumentScope.Project, projectId, null, "Docs", null), default));
    }

    [Fact]
    public async Task Editor_can_create_a_project_folder()
    {
        var owner = await SeedUser("owner");
        var editor = await SeedUser("editor");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, editor, ProjectRole.Editor);

        User.UserId = editor;
        var folders = TestProviders.Folders(Db, User, Clock);
        var created = await folders.CreateAsync(
            new CreateFolderRequest(DocumentScope.Project, projectId, null, "Docs", null), default);
        Assert.Equal(projectId, created.ProjectId);
    }

    [Fact]
    public async Task Non_owner_cannot_edit_a_personal_folder()
    {
        var owner = await SeedUser("owner");
        var other = await SeedUser("other");
        Db.Folders.Add(Seed.Folder(Guid.NewGuid(), "Mine", DocumentScope.Personal, owner, null));
        await Db.SaveChangesAsync();

        User.UserId = other;
        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            folders.UpdateAsync(
                Db.Folders.Single(f => f.OwnerUserId == owner).Id,
                new UpdateFolderRequest("Renamed", null, null, null), default));
    }

    [Fact]
    public async Task Viewer_cannot_edit_a_project_folder()
    {
        var owner = await SeedUser("owner");
        var viewer = await SeedUser("viewer");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, viewer, ProjectRole.Viewer);
        Db.Folders.Add(Seed.Folder(Guid.NewGuid(), "Docs", DocumentScope.Project, null, projectId));
        await Db.SaveChangesAsync();

        User.UserId = viewer;
        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            folders.UpdateAsync(
                Db.Folders.Single(f => f.ProjectId == projectId).Id,
                new UpdateFolderRequest("Renamed", null, null, null), default));
    }

    [Fact]
    public async Task Delete_is_blocked_when_the_folder_has_a_child()
    {
        User.UserId = await SeedUser("owner");
        var parent = Seed.Folder(Guid.NewGuid(), "Parent", DocumentScope.Personal, User.UserId, null);
        var child = Seed.Folder(Guid.NewGuid(), "Child", DocumentScope.Personal, User.UserId, null, parent.Id);
        Db.Folders.AddRange(parent, child);
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ConflictException>(() => folders.DeleteAsync(parent.Id, default));
    }

    [Fact]
    public async Task Delete_is_blocked_when_the_folder_contains_a_document()
    {
        User.UserId = await SeedUser("owner");
        var folder = Seed.Folder(Guid.NewGuid(), "Parent", DocumentScope.Personal, User.UserId, null);
        Db.Folders.Add(folder);
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), User.UserId, DocumentScope.Personal, null, 1));
        await Db.SaveChangesAsync();
        Db.KnowledgeItems.Single().FolderId = folder.Id;
        await Db.SaveChangesAsync();

        var folders = TestProviders.Folders(Db, User, Clock);
        await Assert.ThrowsAsync<ConflictException>(() => folders.DeleteAsync(folder.Id, default));
    }
}

// ---------------------------------------------------------------------------
// Project membership management invariants
// ---------------------------------------------------------------------------
public sealed class ProjectMemberInvariantTests : FolderProjectInvariantTestBase
{
    private ProjectProvider Projects() => new(Db, User, Clock, new NoopProjectMemoryProvider());

    [Fact]
    public async Task Editor_cannot_add_a_member()
    {
        var owner = await SeedUser("owner");
        var editor = await SeedUser("editor");
        var newcomer = await SeedUser("newcomer");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, editor, ProjectRole.Editor);

        User.UserId = editor;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Projects().AddMemberAsync(projectId, new AddProjectMemberRequest(newcomer, ProjectRole.Editor), default));
    }

    [Fact]
    public async Task Adding_an_existing_member_is_rejected()
    {
        var owner = await SeedUser("owner");
        var member = await SeedUser("member");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, member, ProjectRole.Editor);

        User.UserId = owner;
        await Assert.ThrowsAsync<ConflictException>(() =>
            Projects().AddMemberAsync(projectId, new AddProjectMemberRequest(member, ProjectRole.Viewer), default));
    }

    [Fact]
    public async Task Admin_cannot_assign_the_owner_role_to_another_user()
    {
        var owner = await SeedUser("owner");
        var admin = await SeedUser("admin");
        var target = await SeedUser("target");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, admin, ProjectRole.Admin);
        await AddMember(projectId, target, ProjectRole.Editor);

        User.UserId = admin;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Projects().AddMemberAsync(projectId, new AddProjectMemberRequest(target, ProjectRole.Owner), default));
    }

    [Fact]
    public async Task Admin_cannot_change_their_own_membership()
    {
        var owner = await SeedUser("owner");
        var admin = await SeedUser("admin");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, admin, ProjectRole.Admin);

        User.UserId = admin;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Projects().UpdateMemberAsync(projectId, admin, new UpdateProjectMemberRequest(ProjectRole.Editor), default));
    }

    [Fact]
    public async Task Admin_cannot_change_the_owner_role()
    {
        var owner = await SeedUser("owner");
        var admin = await SeedUser("admin");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, admin, ProjectRole.Admin);

        User.UserId = admin;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Projects().UpdateMemberAsync(projectId, owner, new UpdateProjectMemberRequest(ProjectRole.Editor), default));
    }

    [Fact]
    public async Task Demoting_the_only_owner_is_rejected()
    {
        var owner = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = owner;
        await Assert.ThrowsAsync<ValidationException>(() =>
            Projects().UpdateMemberAsync(projectId, owner, new UpdateProjectMemberRequest(ProjectRole.Editor), default));
    }

    [Fact]
    public async Task Removing_the_only_owner_is_rejected()
    {
        var owner = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = owner;
        await Assert.ThrowsAsync<ValidationException>(() => Projects().RemoveMemberAsync(projectId, owner, default));
    }

    [Fact]
    public async Task Owner_cannot_unfollow_a_project_they_own()
    {
        var owner = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = owner;
        await Assert.ThrowsAsync<ValidationException>(() => Projects().UnfollowAsync(projectId, default));
    }

    [Fact]
    public async Task Following_an_archived_project_is_rejected()
    {
        var owner = await SeedUser("owner");
        var follower = await SeedUser("follower");
        var (projectId, _) = await SeedProject("Proj", owner);
        Db.Projects.Single(p => p.Id == projectId).IsArchived = true;
        await Db.SaveChangesAsync();

        User.UserId = follower;
        await Assert.ThrowsAsync<ValidationException>(() => Projects().FollowAsync(projectId, default));
    }

    [Fact]
    public async Task Unfollowing_when_not_a_member_is_a_noop()
    {
        var owner = await SeedUser("owner");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = stranger;
        await Projects().UnfollowAsync(projectId, default); // must not throw
        Assert.False(Db.ProjectMembers.Any(m => m.ProjectId == projectId && m.UserId == stranger));
    }
}

// ---------------------------------------------------------------------------
// Project topic invariants
// ---------------------------------------------------------------------------
public sealed class ProjectTopicInvariantTests : FolderProjectInvariantTestBase
{
    private ProjectTopicProvider Topics() => new(Db, User, Clock);

    [Fact]
    public async Task Duplicate_topic_name_within_a_project_is_rejected()
    {
        var owner = await SeedUser("owner");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = owner;
        await Topics().CreateAsync(projectId, new CreateProjectTopicRequest("Roadmap", null), default);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Topics().CreateAsync(projectId, new CreateProjectTopicRequest("roadmap", null), default));
    }

    [Fact]
    public async Task Viewer_cannot_create_a_topic()
    {
        var owner = await SeedUser("owner");
        var viewer = await SeedUser("viewer");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, viewer, ProjectRole.Viewer);

        User.UserId = viewer;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Topics().CreateAsync(projectId, new CreateProjectTopicRequest("Roadmap", null), default));
    }

    [Fact]
    public async Task Non_member_cannot_create_a_topic()
    {
        var owner = await SeedUser("owner");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);

        User.UserId = stranger;
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Topics().CreateAsync(projectId, new CreateProjectTopicRequest("Roadmap", null), default));
    }

    [Fact]
    public async Task Editor_can_create_a_topic()
    {
        var owner = await SeedUser("owner");
        var editor = await SeedUser("editor");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, editor, ProjectRole.Editor);

        User.UserId = editor;
        var topic = await Topics().CreateAsync(projectId, new CreateProjectTopicRequest("Roadmap", null), default);
        Assert.Equal("Roadmap", topic.Name);
    }

    [Fact]
    public async Task Editor_cannot_delete_a_topic()
    {
        var owner = await SeedUser("owner");
        var editor = await SeedUser("editor");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, editor, ProjectRole.Editor);
        Db.ProjectTopics.Add(new ProjectTopic
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Name = "Roadmap",
            NormalizedName = "ROADMAP", CreatedAt = Clock.UtcNow
        });
        await Db.SaveChangesAsync();

        User.UserId = editor;
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Topics().DeleteAsync(projectId, Db.ProjectTopics.Single().Id, default));
    }
}

// ---------------------------------------------------------------------------
// Document access permission matrix (DocumentAccessService)
// ---------------------------------------------------------------------------
public sealed class DocumentAccessPermissionMatrixTests : FolderProjectInvariantTestBase
{
    private IDocumentAccessService Access() => TestProviders.DocAccess(Db, User);

    [Fact]
    public async Task Personal_document_is_editable_only_by_its_owner()
    {
        var owner = await SeedUser("owner");
        var other = await SeedUser("other");
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Personal, null, 1));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = owner;
        Assert.True(await Access().CanEditAsync(docId, default));
        User.UserId = other;
        Assert.False(await Access().CanEditAsync(docId, default));
    }

    [Fact]
    public async Task Project_document_edit_requires_owner_admin_or_editor()
    {
        var owner = await SeedUser("owner");
        var editor = await SeedUser("editor");
        var viewer = await SeedUser("viewer");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, editor, ProjectRole.Editor);
        await AddMember(projectId, viewer, ProjectRole.Viewer);
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Project, projectId, 1));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = owner; Assert.True(await Access().CanEditAsync(docId, default));
        User.UserId = editor; Assert.True(await Access().CanEditAsync(docId, default));
        User.UserId = viewer; Assert.False(await Access().CanEditAsync(docId, default));
        User.UserId = stranger; Assert.False(await Access().CanEditAsync(docId, default));
    }

    [Fact]
    public async Task Project_document_comment_requires_any_membership()
    {
        var owner = await SeedUser("owner");
        var viewer = await SeedUser("viewer");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, viewer, ProjectRole.Viewer);
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Project, projectId, 1));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = viewer; Assert.True(await Access().CanCommentAsync(docId, default));
        User.UserId = stranger; Assert.False(await Access().CanCommentAsync(docId, default));
    }

    [Fact]
    public async Task Deleted_documents_are_not_viewable()
    {
        var owner = await SeedUser("owner");
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Deleted));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = owner;
        Assert.False(await Access().CanViewAsync(docId, default));
    }

    [Fact]
    public async Task CanView_for_a_project_document_requires_membership()
    {
        var owner = await SeedUser("owner");
        var viewer = await SeedUser("viewer");
        var stranger = await SeedUser("stranger");
        var (projectId, _) = await SeedProject("Proj", owner);
        await AddMember(projectId, viewer, ProjectRole.Viewer);
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Project, projectId, 1, KnowledgeItemStatus.Active));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = owner;
        Assert.True(await Access().CanViewAsync(docId, default));
        User.UserId = viewer;
        Assert.True(await Access().CanViewAsync(docId, default));
        User.UserId = stranger;
        Assert.False(await Access().CanViewAsync(docId, default));
    }

    [Fact]
    public async Task CanView_for_a_personal_document_requires_ownership()
    {
        var owner = await SeedUser("owner");
        var other = await SeedUser("other");
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Personal, null, 1, KnowledgeItemStatus.Active));
        await Db.SaveChangesAsync();
        var docId = Db.KnowledgeItems.Single().Id;

        User.UserId = owner;
        Assert.True(await Access().CanViewAsync(docId, default));
        User.UserId = other;
        Assert.False(await Access().CanViewAsync(docId, default));
    }
}

// ---------------------------------------------------------------------------
// Database-level invariants (enforced by CHECK constraints, not just app code)
// ---------------------------------------------------------------------------
public sealed class KnowledgeItemDbInvariantTests : FolderProjectInvariantTestBase
{
    [Fact]
    public async Task Personal_document_cannot_carry_a_project_id()
    {
        var owner = await SeedUser("owner");
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Personal, Guid.NewGuid(), 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Project_document_requires_a_project_id()
    {
        var owner = await SeedUser("owner");
        Db.KnowledgeItems.Add(Seed.Document(Guid.NewGuid(), owner, DocumentScope.Project, null, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }
}
