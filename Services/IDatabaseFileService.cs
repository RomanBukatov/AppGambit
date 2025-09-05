using AppGambit.Models;

namespace AppGambit.Services
{
    public interface IDatabaseFileService
    {
        Task<ImageData> SaveFileAsync(IFormFile file, ImageType type, int? applicationId = null, string? userId = null);
        Task<ImageData?> GetFileAsync(int fileId);
        Task<bool> DeleteFileAsync(int fileId);
        Task<byte[]> GetFileDataAsync(int fileId);
        Task<string> GetFileContentTypeAsync(int fileId);
        Task<string> GetFileNameAsync(int fileId);
        bool IsValidFileType(IFormFile file);
    }
}