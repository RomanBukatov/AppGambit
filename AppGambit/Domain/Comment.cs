using AppGambit.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Comment
    {
        [Key]
        public int CommentId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        public int? ParentCommentId { get; set; }

        [Required]
        public string Content { get; set; } = null!;

        [Required]
        public DateTime CommentDate { get; set; }

        [Required]
        public bool IsApproved { get; set; } = true;

        [Required]
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedDate { get; set; }

        public string? DeletedByUserId { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual Program Program { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("ParentCommentId")]
        public virtual Comment? ParentComment { get; set; }
        public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();

        [ForeignKey("DeletedByUserId")]
        public virtual ApplicationUser? DeletedByUser { get; set; }

        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
} 