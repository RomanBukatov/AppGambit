using AppGambit.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Rating
    {
        [Key]
        public int RatingId { get; set; }

        public int? ProgramId { get; set; }

        public int? CommentId { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string RatingType { get; set; } = null!; // 'Like' or 'Dislike'

        [Required]
        public DateTime RatingDate { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual Program? Program { get; set; }

        [ForeignKey("CommentId")]
        public virtual Comment? Comment { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;
    }
} 