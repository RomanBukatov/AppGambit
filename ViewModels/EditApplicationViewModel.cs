using System.ComponentModel.DataAnnotations;

namespace AppGambit.ViewModels
{
    public class EditApplicationViewModel
    {
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Название приложения обязательно")]
        [StringLength(100, ErrorMessage = "Название не должно превышать 100 символов")]
        public string Name { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Описание обязательно")]
        [StringLength(500, ErrorMessage = "Описание не должно превышать 500 символов")]
        public string Description { get; set; } = string.Empty;
        
        [StringLength(2000, ErrorMessage = "Подробное описание не должно превышать 2000 символов")]
        public string? DetailedDescription { get; set; }
        
        [Required(ErrorMessage = "Версия обязательна")]
        public string Version { get; set; } = "1.0.0";
        
        public string? Category { get; set; }
        
        public string? TagsString { get; set; }
        
        public IFormFile? IconFile { get; set; }
        
        public IFormFile? AppFile { get; set; }
        
        public List<IFormFile>? Screenshots { get; set; }
        
        // Существующие данные
        public string? CurrentIconUrl { get; set; }
        public string? CurrentDownloadUrl { get; set; }
        public List<string> CurrentScreenshots { get; set; } = new List<string>();
        public List<string> CurrentTags { get; set; } = new List<string>();
        
        // Для удаления существующих скриншотов
        public List<string>? ScreenshotsToDelete { get; set; }
        
        // Для сохранения нового порядка скриншотов
        public string? ScreenshotsOrder { get; set; }
    }
}