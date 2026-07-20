using KnowledgeVault.Contracts.ApiKeys;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class DocumentAccessAndActivityProviderTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _dbContext;
    private readonly TestCurrentUserContext _currentUser = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();
    private readonly Guid _followedProjectId = Guid.NewGuid();
    private readonly Guid _otherProjectId = Guid.NewGuid();
    private readonly Guid _projectDocumentId = Guid.NewGuid();
    private readonly Guid _unfollowedProjectDocumentId = Guid.NewGuid();
    private readonly Guid _personalDocumentId = Guid.NewGuid();

    public DocumentAccessAndActivityProviderTests()
    {
        var options = new DbContextOptionsBuilder<KnowledgeVaultDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _dbContext = new KnowledgeVaultDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _dbContext.Database.OpenConnectionAsync();
        await _dbContext.Database.EnsureCreatedAsync();
        await SeedAsync();
        _currentUser.UserId = _currentUserId;
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Authenticated_user_can_view_document_details_without_ownership_or_membership()
    {
        var access = new DocumentAccessService(_dbContext, _currentUser);
        var documents = new DocumentProvider(_dbContext, _currentUser, _clock, access);

        Assert.True(await access.CanViewAsync(_unfollowedProjectDocumentId, CancellationToken.None));
        Assert.True(await access.CanViewAsync(_personalDocumentId, CancellationToken.None));
        Assert.Equal(
            _unfollowedProjectDocumentId,
            (await documents.GetAsync(_unfollowedProjectDocumentId, CancellationToken.None)).Id);
        Assert.False(await access.CanEditAsync(_unfollowedProjectDocumentId, CancellationToken.None));
        Assert.False(await access.CanEditAsync(_personalDocumentId, CancellationToken.None));
    }

    [Fact]
    public async Task Api_key_can_be_created_without_write_scopes()
    {
        var provider = new ApiKeyProvider(_dbContext, _currentUser, _clock);

        var created = await provider.CreateAsync(
            new CreateApiKeyRequest("Read only", [], 30),
            CancellationToken.None);
        var stored = await _dbContext.ApiKeys.SingleAsync(key => key.Id == created.Id);

        Assert.Empty(stored.Scopes);
    }

    [Fact]
    public async Task Activity_counts_revisions_only_from_followed_projects_in_the_users_local_day()
    {
        var access = new DocumentAccessService(_dbContext, _currentUser);
        var provider = new DocumentProvider(_dbContext, _currentUser, _clock, access);

        var activity = await provider.ListProjectActivityAsync(
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeSpan.Zero),
            utcOffsetMinutes: 480,
            projectId: null,
            CancellationToken.None);

        var day = Assert.Single(activity);
        Assert.Equal(new DateOnly(2026, 7, 2), day.Date);
        Assert.Equal(2, day.ChangeCount);
    }

    [Fact]
    public async Task Stats_count_only_documents_categories_and_tags_from_followed_projects()
    {
        var access = new DocumentAccessService(_dbContext, _currentUser);
        var provider = new DocumentProvider(_dbContext, _currentUser, _clock, access);

        var stats = await provider.GetProjectDocumentStatsAsync(CancellationToken.None);

        Assert.Equal(1, stats.DocumentCount);
        Assert.Equal(1, stats.CategoryCount);
        Assert.Equal(1, stats.TagCount);
    }

    private async Task SeedAsync()
    {
        var currentUser = CreateUser(_currentUserId, "current");
        var otherUser = CreateUser(_otherUserId, "other");
        var followedProject = new Project
        {
            Id = _followedProjectId,
            Name = "Followed project",
            OwnerUserId = _otherUserId,
            CreatedAt = _clock.UtcNow
        };
        var otherProject = new Project
        {
            Id = _otherProjectId,
            Name = "Other project",
            OwnerUserId = _otherUserId,
            CreatedAt = _clock.UtcNow
        };
        var projectDocument = CreateDocument(_projectDocumentId, DocumentScope.Project, _followedProjectId);
        var personalDocument = CreateDocument(_personalDocumentId, DocumentScope.Personal, null);
        var otherProjectDocument = CreateDocument(
            _unfollowedProjectDocumentId,
            DocumentScope.Project,
            _otherProjectId);
        var projectCategory = CreateCategory("Project category");
        var personalCategory = CreateCategory("Personal category");
        var projectTag = CreateTag("Project tag");
        var personalTag = CreateTag("Personal tag");
        var otherProjectTag = CreateTag("Other project tag");
        projectDocument.CategoryId = projectCategory.Id;
        personalDocument.CategoryId = personalCategory.Id;
        otherProjectDocument.CategoryId = personalCategory.Id;

        _dbContext.AddRange(
            currentUser,
            otherUser,
            followedProject,
            otherProject,
            projectDocument,
            personalDocument,
            otherProjectDocument,
            projectCategory,
            personalCategory,
            projectTag,
            personalTag,
            otherProjectTag,
            new KnowledgeItemTag { KnowledgeItemId = projectDocument.Id, TagId = projectTag.Id },
            new KnowledgeItemTag { KnowledgeItemId = personalDocument.Id, TagId = personalTag.Id },
            new KnowledgeItemTag
            {
                KnowledgeItemId = otherProjectDocument.Id,
                TagId = otherProjectTag.Id
            });
        _dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = _followedProjectId,
            UserId = _currentUserId,
            Role = ProjectRole.Viewer,
            CreatedAt = _clock.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var followedRevisions = new[]
        {
            CreateRevision(projectDocument.Id, 1, new DateTimeOffset(2026, 7, 1, 17, 0, 0, TimeSpan.Zero)),
            CreateRevision(projectDocument.Id, 2, new DateTimeOffset(2026, 7, 1, 22, 0, 0, TimeSpan.Zero))
        };
        var personalRevision = CreateRevision(
            personalDocument.Id,
            1,
            new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero));
        var otherProjectRevision = CreateRevision(
            otherProjectDocument.Id,
            1,
            new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero));
        _dbContext.KnowledgeItemRevisions.AddRange(
            followedRevisions[0],
            followedRevisions[1],
            personalRevision,
            otherProjectRevision);
        projectDocument.CurrentRevisionId = followedRevisions[1].Id;
        projectDocument.CurrentRevisionNumber = 2;
        personalDocument.CurrentRevisionId = personalRevision.Id;
        personalDocument.CurrentRevisionNumber = 1;
        otherProjectDocument.CurrentRevisionId = otherProjectRevision.Id;
        otherProjectDocument.CurrentRevisionNumber = 1;
        await _dbContext.SaveChangesAsync();
    }

    private KnowledgeItem CreateDocument(Guid id, DocumentScope scope, Guid? projectId)
    {
        return new KnowledgeItem
        {
            Id = id,
            OwnerUserId = _otherUserId,
            Scope = scope,
            ProjectId = projectId,
            DocumentType = DocumentType.General,
            Status = KnowledgeItemStatus.Active,
            CreatedAt = _clock.UtcNow
        };
    }

    private KnowledgeItemRevision CreateRevision(
        Guid documentId,
        int revisionNumber,
        DateTimeOffset createdAt)
    {
        return new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = documentId,
            RevisionNumber = revisionNumber,
            Title = $"Revision {revisionNumber}",
            Content = "# Content",
            CreatedByUserId = _otherUserId,
            CreatedAt = createdAt
        };
    }

    private Category CreateCategory(string name)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedAt = _clock.UtcNow
        };
    }

    private Tag CreateTag(string name)
    {
        return new Tag
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            CreatedAt = _clock.UtcNow
        };
    }

    private static User CreateUser(Guid id, string name)
    {
        return new User
        {
            Id = id,
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = $"{name}@example.test",
            NormalizedEmail = $"{name}@example.test".ToUpperInvariant(),
            PasswordHash = "hash",
            PasswordSalt = "salt",
            CreatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)
        };
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => UserId != Guid.Empty;

        public Guid UserId { get; set; }
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
    }
}
