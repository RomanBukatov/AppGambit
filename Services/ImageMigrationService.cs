using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;

namespace AppGambit.Services
{
    public class ImageMigrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IDatabaseImageService _databaseImageService;
        private readonly ILogger<ImageMigrationService> _logger;

        public ImageMigrationService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IDatabaseImageService databaseImageService,
            ILogger<ImageMigrationService> logger)
        {
            _context = context;
            _environment = environment;
            _databaseImageService = databaseImageService;
            _logger = logger;
        }

        public async Task MigrateAllImagesAsync()
        {
            _logger.LogInformation("Начинаем миграцию изображений из файловой системы в базу данных");

            await MigrateApplicationIconsAsync();
            await MigrateApplicationScreenshotsAsync();
            await MigrateUserProfileImagesAsync();

            _logger.LogInformation("Миграция изображений завершена");
        }

        private async Task MigrateApplicationIconsAsync()
        {
            _logger.LogInformation("Миграция иконок приложений");

            var applications = await _context.Applications
                .Where(a => !string.IsNullOrEmpty(a.IconUrl) && !a.IconImageId.HasValue)
                .ToListAsync();

            foreach (var app in applications)
            {
                try
                {
                    var iconPath = Path.Combine(_environment.WebRootPath, app.IconUrl!.TrimStart('/'));
                    if (File.Exists(iconPath))
                    {
                        var fileBytes = await File.ReadAllBytesAsync(iconPath);
                        var fileName = Path.GetFileName(iconPath);
                        var contentType = GetContentType(fileName);

                        var imageData = new ImageData
                        {
                            FileName = fileName,
                            ContentType = contentType,
                            Data = fileBytes,
                            Size = fileBytes.Length,
                            Type = ImageType.ApplicationIcon,
                            ApplicationId = app.Id,
                            CreatedAt = app.CreatedAt
                        };

                        _context.Images.Add(imageData);
                        await _context.SaveChangesAsync();

                        app.IconImageId = imageData.Id;
                        app.IconUrl = $"/Image/{imageData.Id}";
                        
                        _logger.LogInformation("Мигрирована иконка приложения {AppId}: {FileName}", app.Id, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при миграции иконки приложения {AppId}", app.Id);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task MigrateApplicationScreenshotsAsync()
        {
            _logger.LogInformation("Миграция скриншотов приложений");

            var applications = await _context.Applications
                .Where(a => a.Screenshots.Any())
                .ToListAsync();

            foreach (var app in applications)
            {
                try
                {
                    var newScreenshotUrls = new List<string>();
                    var screenshotImages = new List<ImageData>();

                    foreach (var screenshotUrl in app.Screenshots)
                    {
                        if (!screenshotUrl.StartsWith("/Image/"))
                        {
                            var screenshotPath = Path.Combine(_environment.WebRootPath, screenshotUrl.TrimStart('/'));
                            if (File.Exists(screenshotPath))
                            {
                                var fileBytes = await File.ReadAllBytesAsync(screenshotPath);
                                var fileName = Path.GetFileName(screenshotPath);
                                var contentType = GetContentType(fileName);

                                var imageData = new ImageData
                                {
                                    FileName = fileName,
                                    ContentType = contentType,
                                    Data = fileBytes,
                                    Size = fileBytes.Length,
                                    Type = ImageType.ApplicationScreenshot,
                                    ApplicationId = app.Id,
                                    CreatedAt = app.CreatedAt
                                };

                                _context.Images.Add(imageData);
                                await _context.SaveChangesAsync();

                                screenshotImages.Add(imageData);
                                newScreenshotUrls.Add($"/Image/{imageData.Id}");
                                
                                _logger.LogInformation("Мигрирован скриншот приложения {AppId}: {FileName}", app.Id, fileName);
                            }
                        }
                        else
                        {
                            newScreenshotUrls.Add(screenshotUrl);
                        }
                    }

                    app.Screenshots = newScreenshotUrls;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при миграции скриншотов приложения {AppId}", app.Id);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task MigrateUserProfileImagesAsync()
        {
            _logger.LogInformation("Миграция изображений профилей пользователей");

            var users = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.ProfileImageUrl) && !u.ProfileImageId.HasValue)
                .ToListAsync();

            foreach (var user in users)
            {
                try
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, user.ProfileImageUrl!.TrimStart('/'));
                    if (File.Exists(imagePath))
                    {
                        var fileBytes = await File.ReadAllBytesAsync(imagePath);
                        var fileName = Path.GetFileName(imagePath);
                        var contentType = GetContentType(fileName);

                        var imageData = new ImageData
                        {
                            FileName = fileName,
                            ContentType = contentType,
                            Data = fileBytes,
                            Size = fileBytes.Length,
                            Type = ImageType.UserProfile,
                            UserId = user.Id,
                            CreatedAt = user.CreatedAt
                        };

                        _context.Images.Add(imageData);
                        await _context.SaveChangesAsync();

                        user.ProfileImageId = imageData.Id;
                        user.ProfileImageUrl = $"/Image/Profile/{user.Id}";
                        
                        _logger.LogInformation("Мигрировано изображение профиля пользователя {UserId}: {FileName}", user.Id, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при миграции изображения профиля пользователя {UserId}", user.Id);
                }
            }

            await _context.SaveChangesAsync();
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                _ => "image/webp"
            };
        }
    }
}