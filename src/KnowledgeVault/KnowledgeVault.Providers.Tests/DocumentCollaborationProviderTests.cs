using KnowledgeVault.Contracts.Comments;
using KnowledgeVault.Contracts.Documents;
using KnowledgeVault.Contracts.Reviews;
using KnowledgeVault.Contracts.Security;
using KnowledgeVault.DataAccess;
using KnowledgeVault.Domain.Entities;
using KnowledgeVault.Domain.Enums;
using KnowledgeVault.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeVault.Providers.Tests;

public sealed class DocumentCollaborationProviderTests : IAsyncLifetime
{
    private readonly KnowledgeVaultDbContext _dbContext;
    private readonly TestCurrentUserContext _currentUser = new();
    private readonly TestDateTimeProvider _clock = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _reviewerId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _documentId = Guid.NewGuid();

    public DocumentCollaborationProviderTests()
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
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Review_is_pinned_to_revision_and_assigned_reviewer_can_approve()
    {
        _currentUser.UserId = _ownerId;
        var providers = CreateProviders();

        var created = await providers.Reviews.CreateAsync(
            _documentId,
            new CreateDocumentReviewRequest(1, [_reviewerId], "Please review the plan."),
            CancellationToken.None);

        var review = Assert.Single(created);
        Assert.Equal(DocumentReviewStatus.Pending, review.Status);
        Assert.Equal(1, review.RevisionNumber);
        Assert.True(review.IsCurrentRevision);

        _currentUser.UserId = _reviewerId;
        var assigned = await providers.Reviews.ListAsync(
            new DocumentReviewQuery(_projectId, null, DocumentReviewStatus.Pending, AssignedToMe: true),
            CancellationToken.None);
        Assert.Single(assigned.Items);

        var decided = await providers.Reviews.DecideAsync(
            review.Id,
            new DecideDocumentReviewRequest(DocumentReviewDecision.Approved, "Looks good."),
            CancellationToken.None);

        Assert.Equal(DocumentReviewStatus.Approved, decided.Status);
        Assert.Equal("Looks good.", decided.DecisionComment);
        Assert.NotNull(decided.ReviewedAt);
    }

    [Fact]
    public async Task Comments_support_replies_and_editor_resolution()
    {
        _currentUser.UserId = _reviewerId;
        var providers = CreateProviders();
        var parent = await providers.Comments.AddAsync(
            _documentId,
            1,
            new AddCommentRequest("Please clarify the rollout."),
            CancellationToken.None);
        var reply = await providers.Comments.AddAsync(
            _documentId,
            1,
            new AddCommentRequest("I will add a staged rollout.", parent.Id),
            CancellationToken.None);

        Assert.Equal(parent.Id, reply.ParentCommentId);

        _currentUser.UserId = _ownerId;
        var resolved = await providers.Comments.ResolveAsync(
            parent.Id,
            new ResolveCommentRequest(true),
            CancellationToken.None);

        Assert.True(resolved.IsResolved);
        Assert.Equal(_ownerId, resolved.ResolvedByUserId);
        Assert.NotNull(resolved.ResolvedAt);
    }

    private ProviderSet CreateProviders()
    {
        var access = new DocumentAccessService(_dbContext, _currentUser);
        var documents = new DocumentProvider(_dbContext, _currentUser, _clock, access);
        var revisions = new RevisionProvider(_dbContext, access);
        var comments = new CommentProvider(_dbContext, _currentUser, _clock, access);
        var reviews = new DocumentReviewProvider(
            _dbContext,
            _currentUser,
            _clock,
            access,
            documents,
            revisions,
            comments);
        return new ProviderSet(comments, reviews);
    }

    private async Task SeedAsync()
    {
        var owner = CreateUser(_ownerId, "owner");
        var reviewer = CreateUser(_reviewerId, "reviewer");
        var project = new Project
        {
            Id = _projectId,
            Name = "Collaboration project",
            OwnerUserId = _ownerId,
            CreatedAt = _clock.UtcNow
        };
        var item = new KnowledgeItem
        {
            Id = _documentId,
            OwnerUserId = _ownerId,
            Scope = DocumentScope.Project,
            ProjectId = _projectId,
            DocumentType = DocumentType.PlanningReview,
            Status = KnowledgeItemStatus.Draft,
            CreatedAt = _clock.UtcNow
        };

        _dbContext.AddRange(owner, reviewer, project, item);
        _dbContext.ProjectMembers.AddRange(
            new ProjectMember
            {
                ProjectId = _projectId,
                UserId = _ownerId,
                Role = ProjectRole.Owner,
                CreatedAt = _clock.UtcNow
            },
            new ProjectMember
            {
                ProjectId = _projectId,
                UserId = _reviewerId,
                Role = ProjectRole.Viewer,
                CreatedAt = _clock.UtcNow
            });
        await _dbContext.SaveChangesAsync();

        var revision = new KnowledgeItemRevision
        {
            Id = Guid.NewGuid(),
            KnowledgeItemId = _documentId,
            RevisionNumber = 1,
            Title = "Review plan",
            Content = "# Plan",
            CreatedByUserId = _ownerId,
            CreatedAt = _clock.UtcNow
        };
        _dbContext.KnowledgeItemRevisions.Add(revision);
        item.CurrentRevisionId = revision.Id;
        item.CurrentRevisionNumber = 1;
        await _dbContext.SaveChangesAsync();
    }

    private User CreateUser(Guid id, string name)
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
            CreatedAt = _clock.UtcNow
        };
    }

    private sealed record ProviderSet(CommentProvider Comments, DocumentReviewProvider Reviews);

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public bool IsAuthenticated => UserId != Guid.Empty;

        public Guid UserId { get; set; }
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);
    }
}
