using System.ComponentModel.DataAnnotations;

namespace AppGambit.Models
{
    public class Application
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000)]
        public string? DetailedDescription { get; set; }
        
        [Required]
        public string Version { get; set; } = "1.0";
        
        public string? IconUrl { get; set; }
        
        // Новые поля для хранения изображений в БД
        public int? IconImageId { get; set; }
        public virtual ImageData? IconImage { get; set; }
        
        public List<string> Screenshots { get; set; } = new List<string>();
        
        // Связь со скриншотами в БД
        public virtual ICollection<ImageData> ScreenshotImages { get; set; } = new List<ImageData>();
        
        public string DownloadUrl { get; set; } = string.Empty;
        
        // Новое поле для хранения файла приложения в БД
        public int? AppFileId { get; set; }
        public virtual ImageData? AppFile { get; set; }
        
        public long FileSize { get; set; }
        
        public string? Category { get; set; }
        
        public List<string> Tags { get; set; } = new List<string>();
        
        public int DownloadCount { get; set; } = 0;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Внешний ключ
        public string UserId { get; set; } = string.Empty;
        
        // Навигационные свойства
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
        
        // Вычисляемые свойства
        public double AverageRating => Ratings?.Any() == true ? Ratings.Average(r => r.Value) : 0;
        public int TotalRatings => Ratings?.Count ?? 0;
        public int LikesCount => Ratings?.Count(r => r.IsLike) ?? 0;
        public int DislikesCount => Ratings?.Count(r => !r.IsLike) ?? 0;

        // Свойство для получения URL скриншотов
        public List<string> ScreenshotUrls => ScreenshotImages?.Select(img => $"/Image/{img.Id}").ToList() ?? new List<string>();
        
        // Свойство для обработки тегов в форме
        public string TagsString
        {
            get => Tags != null ? string.Join(",", Tags) : string.Empty;
            set => Tags = !string.IsNullOrEmpty(value) ? value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList() : new List<string>();
        }
    }
}