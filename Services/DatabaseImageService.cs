using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using AppGambit.Data;
using AppGambit.Models;

namespace AppGambit.Services
{
    public class DatabaseImageService : IDatabaseImageService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseImageService> _logger;
        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico" };

        public DatabaseImageService(ApplicationDbContext context, ILogger<DatabaseImageService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ImageData> SaveImageAsync(IFormFile image, ImageType type, int? applicationId = null, string? userId = null, int maxWidth = 800, int maxHeight = 600)
        {
            try
            {
                if (image == null || image.Length == 0)
                    throw new ArgumentException("Изображение не может быть пустым");

                if (!IsValidImageType(image))
                    throw new ArgumentException("Недопустимый тип изображения");

                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
                byte[] imageData;
                string contentType;
                int? width = null;
                int? height = null;

                // Для ICO файлов сохраняем в оригинальном формате
                if (extension == ".ico")
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await image.CopyToAsync(memoryStream);
                        imageData = memoryStream.ToArray();
                    }
                    contentType = "image/x-icon";
                }
                else
                {
                    // Для остальных форматов конвертируем в WebP и изменяем размер
                    using (var imageStream = image.OpenReadStream())
                    using (var imageSharp = await Image.LoadAsync(imageStream))
                    {
                        width = imageSharp.Width;
                        height = imageSharp.Height;

                        // Изменение размера если необходимо
                        if (imageSharp.Width > maxWidth || imageSharp.Height > maxHeight)
                        {
                            imageSharp.Mutate(x => x.Resize(new ResizeOptions
                            {
                                Size = new Size(maxWidth, maxHeight),
                                Mode = ResizeMode.Max
                            }));
                            
                            width = imageSharp.Width;
                            height = imageSharp.Height;
                        }

                        // Конвертация в WebP
                        using (var outputStream = new MemoryStream())
                        {
                            await imageSharp.SaveAsync(outputStream, new WebpEncoder
                            {
                                Quality = 85
                            });
                            imageData = outputStream.ToArray();
                        }
                    }
                    contentType = "image/webp";
                }

                var imageEntity = new ImageData
                {
                    FileName = $"{Guid.NewGuid()}{(extension == ".ico" ? ".ico" : ".webp")}",
                    ContentType = contentType,
                    Data = imageData,
                    Size = imageData.Length,
                    Width = width,
                    Height = height,
                    Type = type,
                    ApplicationId = applicationId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Images.Add(imageEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Изображение {FileName} успешно сохранено в БД с ID {ImageId}", imageEntity.FileName, imageEntity.Id);
                return imageEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении изображения в БД");
                throw;
            }
        }

        public async Task<ImageData?> GetImageAsync(int imageId)
        {
            try
            {
                return await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == imageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении изображения {ImageId} из БД", imageId);
                return null;
            }
        }

        public async Task<bool> DeleteImageAsync(int imageId)
        {
            try
            {
                var image = await _context.Images.FindAsync(imageId);
                if (image == null)
                    return false;

                _context.Images.Remove(image);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Изображение {ImageId} успешно удалено из БД", imageId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении изображения {ImageId} из БД", imageId);
                return false;
            }
        }

        public async Task<byte[]> GetImageDataAsync(int imageId)
        {
            try
            {
                var image = await _context.Images
                    .AsNoTracking()
                    .Select(i => new { i.Id, i.Data })
                    .FirstOrDefaultAsync(i => i.Id == imageId);

                return image?.Data ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных изображения {ImageId}", imageId);
                return Array.Empty<byte>();
            }
        }

        public async Task<string> GetImageContentTypeAsync(int imageId)
        {
            try
            {
                var image = await _context.Images
                    .AsNoTracking()
                    .Select(i => new { i.Id, i.ContentType })
                    .FirstOrDefaultAsync(i => i.Id == imageId);

                return image?.ContentType ?? "image/webp";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении типа изображения {ImageId}", imageId);
                return "image/webp";
            }
        }

        public async Task<List<ImageData>> GetApplicationScreenshotsAsync(int applicationId)
        {
            try
            {
                return await _context.Images
                    .AsNoTracking()
                    .Where(i => i.ApplicationId == applicationId && i.Type == ImageType.ApplicationScreenshot)
                    .OrderBy(i => i.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении скриншотов приложения {ApplicationId}", applicationId);
                return new List<ImageData>();
            }
        }

        public async Task<ImageData?> GetUserProfileImageAsync(string userId)
        {
            try
            {
                return await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == ImageType.UserProfile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении изображения профиля пользователя {UserId}", userId);
                return null;
            }
        }

        public async Task<ImageData?> GetApplicationIconAsync(int applicationId)
        {
            try
            {
                return await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.ApplicationId == applicationId && i.Type == ImageType.ApplicationIcon);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении иконки приложения {ApplicationId}", applicationId);
                return null;
            }
        }

        public bool IsValidImageType(IFormFile file)
        {
            if (file == null) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedImageExtensions.Contains(extension);
        }
    }
}