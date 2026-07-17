using KnowledgeVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.DataAccess;

public sealed class KnowledgeVaultDbContext(DbContextOptions<KnowledgeVaultDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();

    public DbSet<KnowledgeItemRevision> KnowledgeItemRevisions => Set<KnowledgeItemRevision>();

    public DbSet<KnowledgeItemComment> KnowledgeItemComments => Set<KnowledgeItemComment>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<KnowledgeItemTag> KnowledgeItemTags => Set<KnowledgeItemTag>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<ProjectTopic> ProjectTopics => Set<ProjectTopic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            builder.Property(x => x.NormalizedUserName).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
            builder.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
            builder.Property(x => x.PasswordHash).HasMaxLength(256).IsRequired();
            builder.Property(x => x.PasswordSalt).HasMaxLength(128).IsRequired();
            builder.HasIndex(x => x.NormalizedUserName).IsUnique();
            builder.HasIndex(x => x.NormalizedEmail).IsUnique();
        });

        modelBuilder.Entity<Category>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
            builder.Property(x => x.NormalizedName).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(512);
            builder.Property(x => x.Color).HasMaxLength(32);
            builder.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<Tag>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(64).IsRequired();
            builder.Property(x => x.NormalizedName).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Color).HasMaxLength(32);
            builder.HasIndex(x => x.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<KnowledgeItem>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.OwnerUserId).HasColumnName("UserId");
            builder.Property(x => x.Scope).HasConversion<int>();
            builder.Property(x => x.DocumentType).HasConversion<int>();
            builder.Property(x => x.Status).HasConversion<int>();
            builder.HasIndex(x => new { x.OwnerUserId, x.Scope, x.Status });
            builder.HasIndex(x => new { x.TopicId, x.Status });
            builder.HasOne(x => x.OwnerUser)
                .WithMany(x => x.KnowledgeItems)
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Topic)
                .WithMany()
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.Category)
                .WithMany(x => x.KnowledgeItems)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(x => x.CurrentRevision)
                .WithMany()
                .HasForeignKey(x => x.CurrentRevisionId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.HasMany(x => x.Revisions)
                .WithOne(x => x.KnowledgeItem)
                .HasForeignKey(x => x.KnowledgeItemId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.ToTable(tb => tb
                .HasCheckConstraint("CK_KnowledgeItem_TopicScope",
                    "[Scope] = 0 OR ([Scope] = 1 AND [TopicId] IS NOT NULL)"));
        });

        modelBuilder.Entity<KnowledgeItemRevision>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(1024);
            builder.Property(x => x.SourceUrl).HasMaxLength(2048);
            builder.Property(x => x.TicketNo).HasMaxLength(32);
            builder.Property(x => x.TicketUrl).HasMaxLength(2048);
            builder.Property(x => x.ChangeNote).HasMaxLength(1024);
            builder.HasIndex(x => new { x.KnowledgeItemId, x.RevisionNumber }).IsUnique();
            builder.HasOne(x => x.KnowledgeItem)
                .WithMany(x => x.Revisions)
                .HasForeignKey(x => x.KnowledgeItemId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<KnowledgeItemComment>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            builder.HasIndex(x => new { x.KnowledgeItemRevisionId, x.CreatedAt });
            builder.HasOne(x => x.Revision)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.KnowledgeItemRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.AuthorUser)
                .WithMany()
                .HasForeignKey(x => x.AuthorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ApiKey>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Prefix).HasMaxLength(32).IsRequired();
            builder.Property(x => x.SecretHash).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Scopes).HasMaxLength(256);
            builder.HasIndex(x => x.Prefix);
            builder.HasIndex(x => new { x.UserId, x.RevokedAt });
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<KnowledgeItemTag>(builder =>
        {
            builder.HasKey(x => new { x.KnowledgeItemId, x.TagId });
            builder.HasOne(x => x.KnowledgeItem)
                .WithMany(x => x.KnowledgeItemTags)
                .HasForeignKey(x => x.KnowledgeItemId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Tag)
                .WithMany(x => x.KnowledgeItemTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Project>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(512);
            builder.HasIndex(x => x.OwnerUserId);
            builder.HasMany(x => x.Members)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectMember>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Role).HasConversion<int>();
            builder.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique();
            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ProjectTopic>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
            builder.Property(x => x.NormalizedName).HasMaxLength(128).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(512);
            builder.HasIndex(x => new { x.ProjectId, x.NormalizedName }).IsUnique();
            builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
            builder.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
