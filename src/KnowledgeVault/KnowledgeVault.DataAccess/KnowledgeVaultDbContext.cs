using KnowledgeVault.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeVault.DataAccess;

public sealed class KnowledgeVaultDbContext(DbContextOptions<KnowledgeVaultDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<KnowledgeItem> KnowledgeItems => Set<KnowledgeItem>();

    public DbSet<KnowledgeItemTag> KnowledgeItemTags => Set<KnowledgeItemTag>();

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
            builder.HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
            builder.HasOne(x => x.User)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(64).IsRequired();
            builder.Property(x => x.NormalizedName).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Color).HasMaxLength(32);
            builder.HasIndex(x => new { x.UserId, x.NormalizedName }).IsUnique();
            builder.HasOne(x => x.User)
                .WithMany(x => x.Tags)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeItem>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Title).HasMaxLength(256).IsRequired();
            builder.Property(x => x.Content).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(1024);
            builder.Property(x => x.SourceUrl).HasMaxLength(2048);
            builder.Property(x => x.Status).HasConversion<int>();
            builder.HasIndex(x => new { x.UserId, x.Status });
            builder.HasIndex(x => new { x.UserId, x.CategoryId });
            builder.HasOne(x => x.User)
                .WithMany(x => x.KnowledgeItems)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Category)
                .WithMany(x => x.KnowledgeItems)
                .HasForeignKey(x => x.CategoryId)
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
    }
}
