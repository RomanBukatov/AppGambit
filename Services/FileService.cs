namespace AppGambit.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileService> _logger;

        public FileService(IWebHostEnvironment environment, ILogger<FileService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("Файл не может быть пустым");

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                return Path.Combine("uploads", folder, uniqueFileName).Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении файла");
                throw;
            }
        }

        public async Task<bool> DeleteFileAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                if (File.Exists(fullPath))
                {
                    await Task.Run(() => File.Delete(fullPath));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла: {FilePath}", filePath);
                return false;
            }
        }

        public string GetFileUrl(string filePath)
        {
            return $"/{filePath}";
        }

        public long GetFileSize(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath);
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    return fileInfo.Length;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        public bool IsValidFileType(IFormFile file, string[] allowedExtensions)
        {
            if (file == null) return false;
            
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return allowedExtensions.Contains(extension);
        }
    }
}