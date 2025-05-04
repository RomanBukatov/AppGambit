using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Screenshot
    {
        [Key]
        public int ScreenshotId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        [Required]
        [StringLength(255)]
        public string ImageUrl { get; set; } = null!;

        [Required]
        public int Order { get; set; }

        // Navigation property
        [ForeignKey("ProgramId")]
        public virtual Program Program { get; set; } = null!;
    }
} 