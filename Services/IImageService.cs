namespace AppGambit.Services
{
    public interface IImageService
    {
        Task<string> SaveImageAsync(IFormFile image, string folder, int maxWidth = 800, int maxHeight = 600);
        Task<string> ConvertToWebPAsync(string imagePath);
        bool IsValidImageType(IFormFile file);
        Task<string> ResizeImageAsync(string imagePath, int width, int height);
    }
}