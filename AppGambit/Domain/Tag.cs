using System.ComponentModel.DataAnnotations;

namespace AppGambit.Domain
{
    public class Tag
    {
        [Key]
        public int TagId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; } = null!;

        // Navigation property
        public virtual ICollection<ProgramTag> ProgramTags { get; set; } = new List<ProgramTag>();
    }
} 