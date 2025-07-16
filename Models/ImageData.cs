using System.ComponentModel.DataAnnotations;

namespace AppGambit.Models
{
    public class ImageData
    {
        public int Id { get; set; }
        
        [Required]
        public string FileName { get; set; } = string.Empty;
        
        [Required]
        public string ContentType { get; set; } = string.Empty;
        
        [Required]
        public byte[] Data { get; set; } = Array.Empty<byte>();
        
        public long Size { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Опциональные поля для оптимизации
        public int? Width { get; set; }
        public int? Height { get; set; }
        
        // Связь с приложением (если это иконка или скриншот приложения)
        public int? ApplicationId { get; set; }
        public virtual Models.Application? Application { get; set; }
        
        // Связь с пользователем (если это изображение профиля)
        public string? UserId { get; set; }
        public virtual User? User { get; set; }
        
        // Тип изображения для категоризации
        public ImageType Type { get; set; }
    }
    
    public enum ImageType
    {
        ApplicationIcon = 1,
        ApplicationScreenshot = 2,
        UserProfile = 3,
        ApplicationFile = 4
    }
}