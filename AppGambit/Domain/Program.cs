using AppGambit.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Program
    {
        [Key]
        public int ProgramId { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; } = null!;

        [Required]
        public string Description { get; set; } = null!;

        [Required]
        public DateTime UploadDate { get; set; }

        [StringLength(50)]
        public string? Version { get; set; }

        public long? FileSize { get; set; }

        [Required]
        [StringLength(255)]
        public string DownloadUrl { get; set; } = null!;

        public string? AuthorId { get; set; }

        public double? AverageRating { get; set; }

        // Navigation properties
        [ForeignKey("AuthorId")]
        public virtual ApplicationUser? Author { get; set; }

        public virtual ICollection<Screenshot> Screenshots { get; set; } = new List<Screenshot>();
        public virtual ICollection<ProgramTag> ProgramTags { get; set; } = new List<ProgramTag>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        public virtual ICollection<Download> Downloads { get; set; } = new List<Download>();
        public virtual ICollection<ProgramSubscription> Subscriptions { get; set; } = new List<ProgramSubscription>();
    }
} 