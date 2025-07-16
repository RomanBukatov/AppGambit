using AppGambit.Models;

namespace AppGambit.Services
{
    public interface IDatabaseImageService
    {
        Task<ImageData> SaveImageAsync(IFormFile image, ImageType type, int? applicationId = null, string? userId = null, int maxWidth = 800, int maxHeight = 600);
        Task<ImageData?> GetImageAsync(int imageId);
        Task<bool> DeleteImageAsync(int imageId);
        Task<byte[]> GetImageDataAsync(int imageId);
        Task<string> GetImageContentTypeAsync(int imageId);
        Task<List<ImageData>> GetApplicationScreenshotsAsync(int applicationId);
        Task<ImageData?> GetUserProfileImageAsync(string userId);
        Task<ImageData?> GetApplicationIconAsync(int applicationId);
        bool IsValidImageType(IFormFile file);
    }
}