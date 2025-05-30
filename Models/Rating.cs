using System.ComponentModel.DataAnnotations;

namespace AppGambit.Models
{
    public class Rating
    {
        public int Id { get; set; }
        
        [Range(1, 5)]
        public int Value { get; set; } // Рейтинг от 1 до 5
        
        public bool IsLike { get; set; } // true для лайка, false для дизлайка
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Внешние ключи
        public int ApplicationId { get; set; }
        public string UserId { get; set; } = string.Empty;
        
        // Навигационные свойства
        public virtual Application Application { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}