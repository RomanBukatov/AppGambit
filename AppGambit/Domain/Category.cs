using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AppGambit.Domain
{
    public class Category
    {
        [Key]
        public int CategoryId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        public int? ParentCategoryId { get; set; }

        // Self-reference navigation properties
        [ForeignKey("ParentCategoryId")]
        public virtual Category? ParentCategory { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();

        // Navigation property for programs
        public virtual ICollection<Program> Programs { get; set; } = new List<Program>();
    }
} 