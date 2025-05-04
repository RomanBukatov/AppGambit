using AppGambit.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppGambit.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UserProfile> UserProfiles { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<SoftwareProgram> Programs { get; set; } = null!;
        public DbSet<Screenshot> Screenshots { get; set; } = null!;
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<ProgramTag> ProgramTags { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Rating> Ratings { get; set; } = null!;
        public DbSet<Download> Downloads { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<ProgramSubscription> ProgramSubscriptions { get; set; } = null!;
        public DbSet<AboutCreatorInfo> AboutCreatorInfo { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Customize ASP.NET Identity table names to match your SQL script
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");
            modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles");
            modelBuilder.Entity<IdentityUserRole<string>>().ToTable("AspNetUserRoles");
            modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("AspNetUserClaims");
            modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("AspNetUserLogins");
            modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims");
            modelBuilder.Entity<IdentityUserToken<string>>().ToTable("AspNetUserTokens");

            // Add snake_case table naming convention for all other entities
            modelBuilder.Entity<UserProfile>().ToTable("user_profiles");
            modelBuilder.Entity<Category>().ToTable("categories");
            modelBuilder.Entity<SoftwareProgram>().ToTable("programs");
            modelBuilder.Entity<Screenshot>().ToTable("screenshots");
            modelBuilder.Entity<Tag>().ToTable("tags");
            modelBuilder.Entity<ProgramTag>().ToTable("program_tags");
            modelBuilder.Entity<Comment>().ToTable("comments");
            modelBuilder.Entity<Rating>().ToTable("ratings");
            modelBuilder.Entity<Download>().ToTable("downloads");
            modelBuilder.Entity<Notification>().ToTable("notifications");
            modelBuilder.Entity<ProgramSubscription>().ToTable("program_subscriptions");
            modelBuilder.Entity<AboutCreatorInfo>().ToTable("about_creator_info");

            // Явное указание связей
            // ApplicationUser -> Comment
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Comments)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Comment -> удаленные ApplicationUser
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.DeletedByUser)
                .WithMany()
                .HasForeignKey(c => c.DeletedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ApplicationUser -> Rating
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Ratings)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser -> Download
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Downloads)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ApplicationUser -> Notification
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Notifications)
                .WithOne(n => n.User)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser -> ProgramSubscription
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.ProgramSubscriptions)
                .WithOne(ps => ps.User)
                .HasForeignKey(ps => ps.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ApplicationUser -> SoftwareProgram (авторство)
            modelBuilder.Entity<ApplicationUser>()
                .HasMany(u => u.Programs)
                .WithOne(p => p.Author)
                .HasForeignKey(p => p.AuthorId)
                .OnDelete(DeleteBehavior.SetNull);

            // UserProfile -> ApplicationUser (один-к-одному)
            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId);

            // Configure ProgramTag composite key
            modelBuilder.Entity<ProgramTag>()
                .HasKey(pt => new { pt.ProgramId, pt.TagId });

            // Configure Rating constraints and unique indexes
            modelBuilder.Entity<Rating>()
                .HasCheckConstraint("CK_Rating_Target", 
                    "(\"ProgramId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"ProgramId\" IS NULL AND \"CommentId\" IS NOT NULL)");

            modelBuilder.Entity<Rating>()
                .HasIndex(r => new { r.UserId, r.ProgramId })
                .HasFilter("\"CommentId\" IS NULL")
                .IsUnique();

            modelBuilder.Entity<Rating>()
                .HasIndex(r => new { r.UserId, r.CommentId })
                .HasFilter("\"ProgramId\" IS NULL")
                .IsUnique();

            // Configure relationships
            modelBuilder.Entity<ProgramTag>()
                .HasOne(pt => pt.Program)
                .WithMany(p => p.ProgramTags)
                .HasForeignKey(pt => pt.ProgramId);

            modelBuilder.Entity<ProgramTag>()
                .HasOne(pt => pt.Tag)
                .WithMany(t => t.ProgramTags)
                .HasForeignKey(pt => pt.TagId);

            // Configure self-reference in Comment
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.ParentComment)
                .WithMany(c => c.Replies)
                .HasForeignKey(c => c.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure ProgramSubscription unique constraint
            modelBuilder.Entity<ProgramSubscription>()
                .HasIndex(ps => new { ps.UserId, ps.ProgramId })
                .IsUnique();

            // Configure snake_case column names
            modelBuilder.Entity<UserProfile>().Property(u => u.UserId).HasColumnName("user_id");
            modelBuilder.Entity<UserProfile>().Property(u => u.DisplayName).HasColumnName("display_name");
            modelBuilder.Entity<UserProfile>().Property(u => u.AvatarUrl).HasColumnName("avatar_url");
            modelBuilder.Entity<UserProfile>().Property(u => u.Bio).HasColumnName("bio");
            modelBuilder.Entity<UserProfile>().Property(u => u.WebsiteUrl).HasColumnName("website_url");
            modelBuilder.Entity<UserProfile>().Property(u => u.Location).HasColumnName("location");

            modelBuilder.Entity<ApplicationUser>().Property(u => u.RegistrationDate).HasColumnName("registration_date");
            modelBuilder.Entity<ApplicationUser>().Property(u => u.LastLoginDate).HasColumnName("last_login_date");
            modelBuilder.Entity<ApplicationUser>().Property(u => u.IsActive).HasColumnName("is_active");

            // Custom AboutCreatorInfo configuration
            modelBuilder.Entity<AboutCreatorInfo>()
                .HasData(new AboutCreatorInfo
                {
                    InfoId = 1,
                    CreatorName = "AppGambit",
                    ContactEmail = "contact@appgambit.com",
                    AboutText = "Software catalog for Windows applications.",
                    LastUpdated = DateTime.UtcNow
                });
        }
    }
} 