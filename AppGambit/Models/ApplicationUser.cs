using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AppGambit.Models
{
    // Наследуемся от IdentityUser для минимального набора полей
    public class ApplicationUser : IdentityUser
    {
        // Сохраняем только нужные дополнительные поля
        public DateTime RegistrationDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public virtual UserProfile? Profile { get; set; }
        public virtual ICollection<SoftwareProgram> Programs { get; set; } = new List<SoftwareProgram>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public virtual ICollection<Download> Downloads { get; set; } = new List<Download>();
        public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        public virtual ICollection<ProgramSubscription> ProgramSubscriptions { get; set; } = new List<ProgramSubscription>();
    }

    // Класс для настройки схемы ApplicationUser в DbContext
    public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
    {
        public void Configure(EntityTypeBuilder<ApplicationUser> builder)
        {
            // Исключаем ненужные поля из схемы
            builder.Ignore(u => u.PhoneNumber);
            builder.Ignore(u => u.PhoneNumberConfirmed);
            builder.Ignore(u => u.TwoFactorEnabled);
            builder.Ignore(u => u.LockoutEnd);
            builder.Ignore(u => u.LockoutEnabled);
            builder.Ignore(u => u.AccessFailedCount);
            builder.Ignore(u => u.SecurityStamp);
            builder.Ignore(u => u.ConcurrencyStamp);
        }
    }
} 