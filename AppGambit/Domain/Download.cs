using AppGambit.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Download
    {
        [Key]
        public int DownloadId { get; set; }

        [Required]
        public int ProgramId { get; set; }

        public string? UserId { get; set; }

        [Required]
        public DateTime DownloadDate { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        // Navigation properties
        [ForeignKey("ProgramId")]
        public virtual Program Program { get; set; } = null!;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
} 