using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AppGambit.Models;
using System.Text.Json;

namespace AppGambit.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Models.Application> Applications { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<ImageData> Images { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Конфигурация для Application
            builder.Entity<Models.Application>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
                entity.Property(e => e.DetailedDescription).HasMaxLength(2000);
                entity.Property(e => e.Version).IsRequired();
                entity.Property(e => e.DownloadUrl);
                
                // Индексы для улучшения производительности
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => new { e.Name, e.Category });
                entity.HasIndex(e => new { e.CreatedAt, e.Category });
                
                // Конвертация списков в JSON
                entity.Property(e => e.Screenshots)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
                
                entity.Property(e => e.Tags)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

                // Связи
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Applications)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Связь с иконкой
                entity.HasOne(e => e.IconImage)
                    .WithOne()
                    .HasForeignKey<Models.Application>(e => e.IconImageId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Связь со скриншотами
                entity.HasMany(e => e.ScreenshotImages)
                    .WithOne(i => i.Application)
                    .HasForeignKey(i => i.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация для Comment
            builder.Entity<Comment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);

                // Индексы для улучшения производительности
                entity.HasIndex(e => e.ApplicationId);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => new { e.ApplicationId, e.CreatedAt });

                entity.HasOne(e => e.Application)
                    .WithMany(a => a.Comments)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Comments)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация для Rating
            builder.Entity<Rating>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Value).IsRequired();

                // Уникальный индекс: один пользователь может оценить приложение только один раз
                entity.HasIndex(e => new { e.ApplicationId, e.UserId }).IsUnique();

                entity.HasOne(e => e.Application)
                    .WithMany(a => a.Ratings)
                    .HasForeignKey(e => e.ApplicationId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.Ratings)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация для User
            builder.Entity<User>(entity =>
            {
                entity.Property(e => e.DisplayName).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt).IsRequired();

                // Связь с изображением профиля
                entity.HasOne(e => e.ProfileImage)
                    .WithOne(i => i.User)
                    .HasForeignKey<User>(e => e.ProfileImageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Конфигурация для ImageData
            builder.Entity<ImageData>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.ContentType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Data).IsRequired();
                entity.Property(e => e.Size).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.Type).IsRequired();

                // Индексы для улучшения производительности
                entity.HasIndex(e => e.ApplicationId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}