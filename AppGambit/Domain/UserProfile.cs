using AppGambit.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class UserProfile
    {
        [Key]
        [ForeignKey("User")]
        public string UserId { get; set; } = null!;

        [StringLength(100)]
        public string? DisplayName { get; set; }

        [StringLength(255)]
        public string? AvatarUrl { get; set; }

        public string? Bio { get; set; }

        [StringLength(255)]
        public string? WebsiteUrl { get; set; }

        [StringLength(100)]
        public string? Location { get; set; }

        // Navigation property
        public virtual ApplicationUser User { get; set; } = null!;
    }
} 