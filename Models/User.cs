using Microsoft.AspNetCore.Identity;

namespace AppGambit.Models
{
    public class User : IdentityUser
    {
        public string? DisplayName { get; set; }
        public string? ProfileImageUrl { get; set; }
        
        // Новое поле для хранения изображения профиля в БД
        public int? ProfileImageId { get; set; }
        public virtual ImageData? ProfileImage { get; set; }
        
        // Настройки пользователя
        public string? PreferredTheme { get; set; } = "light"; // light, dark, auto
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Навигационные свойства
        public virtual ICollection<Application> Applications { get; set; } = new List<Application>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    }
}