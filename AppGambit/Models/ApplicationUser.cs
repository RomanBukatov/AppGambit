using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace AppGambit.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Additional fields from the SQL script's AspNetUsers table
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
} 