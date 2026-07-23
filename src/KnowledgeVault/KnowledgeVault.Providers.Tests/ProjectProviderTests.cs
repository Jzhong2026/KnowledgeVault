using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeVault.Contracts.Projects;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Providers;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

// Safety net for ProjectProvider (483 lines, refactor target). Covers membership,
// role resolution and follow semantics so the upcoming provider split cannot change
// project collaboration behavior.
public sealed class ProjectProviderTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _db = TestDb.Create();
    private readonly FakeCurrentUser _user = new();
    private readonly FakeClock _clock = new();
    private readonly NoopProjectMemoryProvider _memory = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherId = Guid.NewGuid();

    public ProjectProviderTests()
    {
        _user.UserId = _userId;
        _db.Users.AddRange(Seed.User(_userId, "owner"), Seed.User(_otherId, "other"));
        _db.SaveChanges();
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => _db.DisposeAsync().AsTask();

    private ProjectProvider Projects() => new(_db, _user, _clock, _memory);

    [Fact]
    public async Task Create_adds_owner_as_owner_member()
    {
        var created = await Projects().CreateAsync(new CreateProjectRequest("Alpha", null), CancellationToken.None);

        var members = await Projects().ListMembersAsync(created.Id, CancellationToken.None);
        var me = Assert.Single(members.Where(m => m.UserId == _userId));
        Assert.Equal(ProjectRole.Owner, me.Role);
    }

    [Fact]
    public async Task Get_returns_caller_role()
    {
        var projectId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _otherId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Editor));
        await _db.SaveChangesAsync();

        var dto = await Projects().GetAsync(projectId, CancellationToken.None);
        Assert.Equal(ProjectRole.Editor, dto.CurrentUserRole);
    }

    [Fact]
    public async Task List_following_only_returns_memberships()
    {
        var followed = Guid.NewGuid();
        var notFollowed = Guid.NewGuid();
        _db.Projects.AddRange(Seed.Project(followed, "F", _otherId), Seed.Project(notFollowed, "N", _otherId));
        _db.ProjectMembers.Add(Seed.Member(followed, _userId, ProjectRole.Viewer));
        await _db.SaveChangesAsync();

        var result = await Projects().ListAsync(new ProjectQuery(null, false, true), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(followed, result.Items[0].Id);
    }

    [Fact]
    public async Task Add_and_remove_member()
    {
        var projectId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _userId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _userId, ProjectRole.Owner));
        await _db.SaveChangesAsync();

        await Projects().AddMemberAsync(projectId, new AddProjectMemberRequest(_otherId, ProjectRole.Viewer), CancellationToken.None);
        var afterAdd = await Projects().ListMembersAsync(projectId, CancellationToken.None);
        Assert.Contains(afterAdd, m => m.UserId == _otherId && m.Role == ProjectRole.Viewer);

        await Projects().RemoveMemberAsync(projectId, _otherId, CancellationToken.None);
        var afterRemove = await Projects().ListMembersAsync(projectId, CancellationToken.None);
        Assert.DoesNotContain(afterRemove, m => m.UserId == _otherId);
    }

    [Fact]
    public async Task Get_requires_membership()
    {
        var projectId = Guid.NewGuid();
        _db.Projects.Add(Seed.Project(projectId, "P", _otherId));
        _db.ProjectMembers.Add(Seed.Member(projectId, _otherId, ProjectRole.Owner));
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<KnowledgeVault.Infrastructure.Exceptions.NotFoundException>(() =>
            Projects().GetAsync(projectId, CancellationToken.None));
    }

    [Fact]
    public async Task List_excludes_projects_the_caller_is_not_a_member_of()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        _db.Projects.AddRange(Seed.Project(mine, "Mine", _userId), Seed.Project(theirs, "Theirs", _otherId));
        _db.ProjectMembers.Add(Seed.Member(mine, _userId, ProjectRole.Owner));
        _db.ProjectMembers.Add(Seed.Member(theirs, _otherId, ProjectRole.Owner));
        await _db.SaveChangesAsync();

        var result = await Projects().ListAsync(new ProjectQuery(null, false, false), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(mine, result.Items[0].Id);
    }
}
