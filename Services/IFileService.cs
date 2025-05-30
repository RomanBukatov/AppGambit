namespace AppGambit.Services
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file, string folder);
        Task<bool> DeleteFileAsync(string filePath);
        string GetFileUrl(string filePath);
        long GetFileSize(string filePath);
        bool IsValidFileType(IFormFile file, string[] allowedExtensions);
    }
}