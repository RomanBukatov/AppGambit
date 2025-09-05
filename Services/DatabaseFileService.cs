using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;

namespace AppGambit.Services
{
    public class DatabaseFileService : IDatabaseFileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseFileService> _logger;
        private readonly string[] _allowedFileExtensions = { ".exe", ".msi", ".zip", ".rar", ".7z", ".apk" };

        public DatabaseFileService(ApplicationDbContext context, ILogger<DatabaseFileService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ImageData> SaveFileAsync(IFormFile file, ImageType type, int? applicationId = null, string? userId = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("Файл не может быть пустым");

                if (!IsValidFileType(file))
                    throw new ArgumentException("Недопустимый тип файла");

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                byte[] fileData;

                // Сохраняем файл в оригинальном формате
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                var contentType = GetContentType(extension);

                var fileEntity = new ImageData
                {
                    FileName = $"{Guid.NewGuid()}_{file.FileName}",
                    ContentType = contentType,
                    Data = fileData,
                    Size = fileData.Length,
                    Type = type,
                    ApplicationId = applicationId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Images.Add(fileEntity);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Файл {FileName} успешно сохранен в БД с ID {FileId}", fileEntity.FileName, fileEntity.Id);
                return fileEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении файла в БД");
                throw;
            }
        }

        public async Task<ImageData?> GetFileAsync(int fileId)
        {
            try
            {
                return await _context.Images
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении файла {FileId} из БД", fileId);
                return null;
            }
        }

        public async Task<bool> DeleteFileAsync(int fileId)
        {
            try
            {
                var file = await _context.Images.FindAsync(fileId);
                if (file == null)
                    return false;

                _context.Images.Remove(file);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Файл {FileId} успешно удален из БД", fileId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении файла {FileId} из БД", fileId);
                return false;
            }
        }

        public async Task<byte[]> GetFileDataAsync(int fileId)
        {
            try
            {
                var file = await _context.Images
                    .AsNoTracking()
                    .Select(i => new { i.Id, i.Data })
                    .FirstOrDefaultAsync(i => i.Id == fileId);

                return file?.Data ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных файла {FileId}", fileId);
                return Array.Empty<byte>();
            }
        }

        public async Task<string> GetFileContentTypeAsync(int fileId)
        {
            try
            {
                var file = await _context.Images
                    .AsNoTracking()
                    .Select(i => new { i.Id, i.ContentType })
                    .FirstOrDefaultAsync(i => i.Id == fileId);

                return file?.ContentType ?? "application/octet-stream";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении типа файла {FileId}", fileId);
                return "application/octet-stream";
            }
        }

        public async Task<string> GetFileNameAsync(int fileId)
        {
            try
            {
                var file = await _context.Images
                    .AsNoTracking()
                    .Select(i => new { i.Id, i.FileName })
                    .FirstOrDefaultAsync(i => i.Id == fileId);

                return file?.FileName ?? "download";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении имени файла {FileId}", fileId);
                return "download";
            }
        }

        public bool IsValidFileType(IFormFile file)
        {
            if (file == null) return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            return _allowedFileExtensions.Contains(extension);
        }

        private string GetContentType(string extension)
        {
            return extension switch
            {
                ".exe" => "application/vnd.microsoft.portable-executable",
                ".msi" => "application/x-msi",
                ".zip" => "application/zip",
                ".rar" => "application/vnd.rar",
                ".7z" => "application/x-7z-compressed",
                ".apk" => "application/vnd.android.package-archive",
                _ => "application/octet-stream"
            };
        }
    }
}