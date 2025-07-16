using Microsoft.AspNetCore.Mvc;
using AppGambit.Services;

namespace AppGambit.Controllers
{
    [Route("[controller]")]
    public class ImageController : Controller
    {
        private readonly IDatabaseImageService _databaseImageService;
        private readonly ILogger<ImageController> _logger;

        public ImageController(IDatabaseImageService databaseImageService, ILogger<ImageController> logger)
        {
            _databaseImageService = databaseImageService;
            _logger = logger;
        }

        // GET: Image/{id}
        [HttpGet("{id:int}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "User-Agent")]
        public async Task<IActionResult> GetImage(int id)
        {
            try
            {
                var imageData = await _databaseImageService.GetImageDataAsync(id);
                if (imageData == null || imageData.Length == 0)
                {
                    return NotFound();
                }

                var contentType = await _databaseImageService.GetImageContentTypeAsync(id);
                
                // Добавляем заголовки для кэширования
                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["ETag"] = $"\"{id}\"";
                
                return File(imageData, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении изображения {ImageId}", id);
                return NotFound();
            }
        }

        // GET: Image/Profile/{userId}
        [HttpGet("Profile/{userId}")]
        [ResponseCache(Duration = 1800, Location = ResponseCacheLocation.Any, VaryByHeader = "User-Agent")]
        public async Task<IActionResult> GetProfileImage(string userId)
        {
            try
            {
                var image = await _databaseImageService.GetUserProfileImageAsync(userId);
                if (image == null)
                {
                    return NotFound();
                }

                // Добавляем заголовки для кэширования
                Response.Headers["Cache-Control"] = "public, max-age=1800";
                Response.Headers["ETag"] = $"\"{image.Id}\"";
                
                return File(image.Data, image.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении изображения профиля пользователя {UserId}", userId);
                return NotFound();
            }
        }

        // GET: Image/Icon/{applicationId}
        [HttpGet("Icon/{applicationId:int}")]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, VaryByHeader = "User-Agent")]
        public async Task<IActionResult> GetApplicationIcon(int applicationId)
        {
            try
            {
                var image = await _databaseImageService.GetApplicationIconAsync(applicationId);
                if (image == null)
                {
                    return NotFound();
                }

                // Добавляем заголовки для кэширования
                Response.Headers["Cache-Control"] = "public, max-age=3600";
                Response.Headers["ETag"] = $"\"{image.Id}\"";
                
                return File(image.Data, image.ContentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении иконки приложения {ApplicationId}", applicationId);
                return NotFound();
            }
        }
    }
}