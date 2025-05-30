using System.ComponentModel.DataAnnotations;

namespace AppGambit.Models
{
    public class Comment
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(1000)]
        public string Content { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Внешние ключи
        public int ApplicationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        
        // Навигационные свойства
        public virtual Application Application { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}