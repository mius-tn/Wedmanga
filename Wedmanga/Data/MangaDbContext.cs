using Microsoft.EntityFrameworkCore;
using Wedmanga.Models.Entities;

namespace Wedmanga.Data
{
    public class MangaDbContext : DbContext
    {
        public MangaDbContext(DbContextOptions<MangaDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Story> Stories { get; set; } = null!;
        public DbSet<Chapter> Chapters { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<StoryCategory> StoryCategories { get; set; } = null!;
        public DbSet<Rating> Ratings { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Bookmark> Bookmarks { get; set; } = null!;
        public DbSet<ReadingHistory> ReadingHistories { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Composite Keys
            modelBuilder.Entity<StoryCategory>().HasKey(sc => new { sc.StoryId, sc.CategoryId });

            // Unique Constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Role>().HasIndex(r => r.RoleName).IsUnique();
            modelBuilder.Entity<Story>().HasIndex(s => s.Slug).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();

            modelBuilder.Entity<Chapter>().HasIndex(c => new { c.StoryId, c.ChapterNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            modelBuilder.Entity<Rating>().HasIndex(r => new { r.UserId, r.StoryId }).IsUnique();
            modelBuilder.Entity<Bookmark>().HasIndex(b => new { b.UserId, b.StoryId }).IsUnique();
            modelBuilder.Entity<ReadingHistory>().HasIndex(h => new { h.UserId, h.StoryId }).IsUnique();

            // Relationships
            modelBuilder.Entity<StoryCategory>()
                .HasOne(sc => sc.Story)
                .WithMany(s => s.StoryCategories)
                .HasForeignKey(sc => sc.StoryId);

            modelBuilder.Entity<StoryCategory>()
                .HasOne(sc => sc.Category)
                .WithMany(c => c.StoryCategories)
                .HasForeignKey(sc => sc.CategoryId);

            // Prevent circular cascade paths by setting DeleteBehavior.Restrict for User/Story relations
            modelBuilder.Entity<ReadingHistory>()
                .HasOne(rh => rh.Story)
                .WithMany(s => s.ReadingHistories)
                .HasForeignKey(rh => rh.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<ReadingHistory>()
                .HasOne(rh => rh.User)
                .WithMany(u => u.ReadingHistories)
                .HasForeignKey(rh => rh.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.Story)
                .WithMany(s => s.Bookmarks)
                .HasForeignKey(b => b.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookmarks)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Story)
                .WithMany(s => s.Comments)
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Rating>()
                .HasOne(r => r.Story)
                .WithMany(s => s.Ratings)
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            modelBuilder.Entity<Rating>()
                .HasOne(r => r.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Default values
            modelBuilder.Entity<User>().Property(u => u.Status).HasDefaultValue("Active");
            modelBuilder.Entity<Story>().Property(s => s.Status).HasDefaultValue("Ongoing");

            modelBuilder.Entity<ReadingHistory>()
                .HasOne(rh => rh.Chapter)
                .WithMany(c => c.ReadingHistories)
                .HasForeignKey(rh => rh.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Bookmark>()
                .HasOne(b => b.Chapter)
                .WithMany(c => c.Bookmarks)
                .HasForeignKey(b => b.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Chapter)
                .WithMany(ch => ch.Comments)
                .HasForeignKey(c => c.ChapterId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Triggers to prevent EF Core 7+ OUTPUT clause errors
            modelBuilder.Entity<Rating>().ToTable(tb => tb.HasTrigger("TRG_UpdateStoryRating"));
            modelBuilder.Entity<Chapter>().ToTable(tb => tb.HasTrigger("TRG_UpdateStoryViewCount"));

            // Globally disable Cascade Delete to prevent SQL Server cyclical reference errors
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}
