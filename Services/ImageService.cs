using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;

namespace AppGambit.Services
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImageService> _logger;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };

        public ImageService(IWebHostEnvironment environment, ILogger<ImageService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> SaveImageAsync(IFormFile image, string folder, int maxWidth = 800, int maxHeight = 600)
        {
            try
            {
                if (image == null || image.Length == 0)
                    throw new ArgumentException("Изображение не может быть пустым");

                if (!IsValidImageType(image))
                    throw new ArgumentException("Недопустимый тип изображения");

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", folder);
                Directory.CreateDirectory(uploadsFolder);

                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                
                // Для ICO файлов сохраняем в оригинальном формате
                if (extension == ".ico")
                {
                    var fileName = $"{Guid.NewGuid()}.ico";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(fileStream);
                    }
                    
                    return Path.Combine("uploads", folder, fileName).Replace("\\", "/");
                }
                else
                {
                    // Для остальных форматов конвертируем в WebP
                    var fileName = $"{Guid.NewGuid()}.webp";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var imageStream = image.OpenReadStream())
                    using (var imageSharp = await Image.LoadAsync(imageStream))
                    {
                        // Изменение размера если необходимо
                        if (imageSharp.Width > maxWidth || imageSharp.Height > maxHeight)
                        {
                            imageSharp.Mutate(x => x.Resize(new ResizeOptions
                            {
                                Size = new Size(maxWidth, maxHeight),
                                Mode = ResizeMode.Max
                            }));
                        }

                        // Сохранение в формате WebP
                        await imageSharp.SaveAsync(filePath, new WebpEncoder
                        {
                            Quality = 85
                        });
                    }

                    return Path.Combine("uploads", folder, fileName).Replace("\\", "/");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении изображения");
                throw;
            }
        }

        public async Task<string> ConvertToWebPAsync(string imagePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, imagePath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Изображение не найдено");

                var directory = Path.GetDirectoryName(fullPath);
                var webpFileName = $"{Path.GetFileNameWithoutExtension(fullPath)}.webp";
                var webpPath = Path.Combine(directory!, webpFileName);

                using (var image = await Image.LoadAsync(fullPath))
                {
                    await image.SaveAsync(webpPath, new WebpEncoder
                    {
                        Quality = 85
                    });
                }

                // Удаление оригинального файла
                File.Delete(fullPath);

                var relativePath = Path.GetRelativePath(_environment.WebRootPath, webpPath);
                return relativePath.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при конвертации изображения в WebP");
                throw;
            }
        }

        public bool IsValidImageType(IFormFile file)
        {
            if (file == null) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedImageExtensions.Contains(extension);
        }

        public async Task<string> ResizeImageAsync(string imagePath, int width, int height)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, imagePath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException("Изображение не найдено");

                var directory = Path.GetDirectoryName(fullPath);
                var fileName = Path.GetFileNameWithoutExtension(fullPath);
                var extension = Path.GetExtension(fullPath);
                var resizedFileName = $"{fileName}_{width}x{height}{extension}";
                var resizedPath = Path.Combine(directory!, resizedFileName);

                using (var image = await Image.LoadAsync(fullPath))
                {
                    image.Mutate(x => x.Resize(width, height));
                    await image.SaveAsync(resizedPath);
                }

                var relativePath = Path.GetRelativePath(_environment.WebRootPath, resizedPath);
                return relativePath.Replace("\\", "/");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении размера изображения");
                throw;
            }
        }
    }
}