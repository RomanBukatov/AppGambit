using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;
using AppGambit.Services;
using AppGambit.ViewModels;

namespace AppGambit.Controllers
{
    public class ApplicationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IFileService _fileService;
        private readonly IImageService _imageService;
        private readonly ILogger<ApplicationsController> _logger;

        public ApplicationsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IFileService fileService,
            IImageService imageService,
            ILogger<ApplicationsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _fileService = fileService;
            _imageService = imageService;
            _logger = logger;
        }

        // GET: Applications/Search - глобальный поиск
        public async Task<IActionResult> Search(string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return RedirectToAction(nameof(Index));
            }

            // Проверяем, является ли поиск именем пользователя
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.DisplayName == search);
            
            if (user != null)
            {
                return RedirectToAction("ByDisplayName", "Profile", new { displayName = search });
            }

            // Если не пользователь, перенаправляем на обычный поиск приложений
            return RedirectToAction(nameof(Index), new { search = search });
        }

        // GET: Applications
        public async Task<IActionResult> Index(string search, string category, int page = 1, int pageSize = 12)
        {
            var query = _context.Applications
                .Include(a => a.User)
                .Include(a => a.Ratings)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.Name.Contains(search) ||
                                        a.Description.Contains(search) ||
                                        (a.User.DisplayName != null && a.User.DisplayName.Contains(search)) ||
                                        (a.User.Email != null && a.User.Email.Contains(search)));
                ViewBag.CurrentSearch = search;
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(a => a.Category == category);
                ViewBag.CurrentCategory = category;
            }

            var totalItems = await query.CountAsync();
            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new ApplicationListItemViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    IconUrl = a.IconUrl,
                    CreatedAt = a.CreatedAt,
                    DownloadCount = a.DownloadCount,
                    Category = a.Category,
                    Tags = a.Tags,
                    UserDisplayName = a.User.DisplayName ?? a.User.Email ?? "Неизвестный",
                    AverageRating = a.Ratings.Any() ? a.Ratings.Average(r => r.Value) : 0,
                    TotalRatings = a.Ratings.Count(),
                    LikesCount = a.Ratings.Count(r => r.IsLike),
                    DislikesCount = a.Ratings.Count(r => !r.IsLike)
                })
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.PageSize = pageSize;

            return View(applications);
        }

        // GET: Applications/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var application = await _context.Applications
                .Include(a => a.User)
                .Include(a => a.Comments)
                    .ThenInclude(c => c.User)
                .Include(a => a.Ratings)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            // Получаем рейтинг текущего пользователя
            Rating? userRating = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                userRating = await _context.Ratings
                    .FirstOrDefaultAsync(r => r.ApplicationId == id && r.UserId == userId);
                
                ViewBag.CurrentUserId = userId;
            }

            ViewBag.UserRating = userRating;
            return View(application);
        }

        // GET: Applications/Create
        [Authorize]
        public IActionResult Create()
        {
            return View(new CreateApplicationViewModel());
        }

        // POST: Applications/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(CreateApplicationViewModel model)
        {
            try
            {
                _logger.LogInformation("Начало создания приложения. ModelState.IsValid: {IsValid}", ModelState.IsValid);
                
                // Логируем ошибки валидации
                if (!ModelState.IsValid)
                {
                    foreach (var error in ModelState)
                    {
                        _logger.LogWarning("Ошибка валидации для {Key}: {Errors}", 
                            error.Key, string.Join(", ", error.Value.Errors.Select(e => e.ErrorMessage)));
                    }
                    return View(model);
                }

                var userId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("Не удалось получить ID пользователя");
                    ModelState.AddModelError("", "Ошибка авторизации");
                    return View(model);
                }

                // Создаем объект Application из ViewModel
                var application = new Models.Application
                {
                    Name = model.Name,
                    Description = model.Description,
                    DetailedDescription = model.DetailedDescription,
                    Version = model.Version,
                    Category = model.Category,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    DownloadUrl = "",
                    FileSize = 0
                };

                // Обработка тегов
                if (!string.IsNullOrEmpty(model.TagsString))
                {
                    application.Tags = model.TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList();
                }

                _logger.LogInformation("Создание приложения для пользователя {UserId}: {AppName}", userId, application.Name);

                // Сохранение иконки
                if (model.IconFile != null && _imageService.IsValidImageType(model.IconFile))
                {
                    _logger.LogInformation("Сохранение иконки для приложения {AppName}", application.Name);
                    application.IconUrl = await _imageService.SaveImageAsync(model.IconFile, "icons", 128, 128);
                }

                // Сохранение файла приложения - делаем необязательным
                if (model.AppFile != null)
                {
                    var allowedExtensions = new[] { ".exe", ".msi", ".zip", ".rar", ".7z", ".apk" };
                    if (_fileService.IsValidFileType(model.AppFile, allowedExtensions))
                    {
                        _logger.LogInformation("Сохранение файла приложения {AppName}", application.Name);
                        application.DownloadUrl = await _fileService.SaveFileAsync(model.AppFile, "apps");
                        application.FileSize = model.AppFile.Length;
                    }
                    else
                    {
                        ModelState.AddModelError("AppFile", "Недопустимый тип файла приложения");
                        return View(model);
                    }
                }

                // Сохранение скриншотов
                if (model.Screenshots != null && model.Screenshots.Any())
                {
                    var screenshotUrls = new List<string>();
                    foreach (var screenshot in model.Screenshots.Where(s => s.Length > 0))
                    {
                        if (_imageService.IsValidImageType(screenshot))
                        {
                            _logger.LogInformation("Сохранение скриншота для приложения {AppName}", application.Name);
                            var url = await _imageService.SaveImageAsync(screenshot, "screenshots");
                            screenshotUrls.Add(url);
                        }
                    }
                    application.Screenshots = screenshotUrls;
                }

                _logger.LogInformation("Добавление приложения в контекст БД: {AppName}", application.Name);
                _context.Applications.Add(application);
                
                _logger.LogInformation("Сохранение изменений в БД для приложения: {AppName}", application.Name);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Приложение успешно создано с ID: {AppId}", application.Id);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании приложения");
                ModelState.AddModelError("", "Произошла ошибка при создании приложения");
            }

            return View(model);
        }

        // POST: Applications/Download
        [HttpPost]
        public async Task<IActionResult> Download(int id)
        {
            var application = await _context.Applications.FindAsync(id);
            if (application == null)
            {
                return NotFound();
            }

            // Увеличиваем счетчик скачиваний
            application.DownloadCount++;
            await _context.SaveChangesAsync();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", application.DownloadUrl);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Файл не найден");
            }

            var fileName = Path.GetFileName(application.DownloadUrl);
            return PhysicalFile(filePath, "application/octet-stream", fileName);
        }

        // POST: Applications/Rate
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Rate(int applicationId, int value, bool isLike)
        {
            var userId = _userManager.GetUserId(User);

            var existingRating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.UserId == userId);

            if (existingRating != null)
            {
                existingRating.Value = value;
                existingRating.IsLike = isLike;
            }
            else
            {
                var rating = new Rating
                {
                    ApplicationId = applicationId,
                    UserId = userId!,
                    Value = value,
                    IsLike = isLike,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Ratings.Add(rating);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST: Applications/AddComment
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddComment(int applicationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return RedirectToAction(nameof(Details), new { id = applicationId });
            }

            var userId = _userManager.GetUserId(User);
            var comment = new Comment
            {
                ApplicationId = applicationId,
                UserId = userId!,
                Content = content.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = applicationId });
        }

        // POST: Applications/UpdateComment
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpdateComment([FromBody] UpdateCommentRequest request)
        {
            try
            {
                var comment = await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == request.CommentId);

                if (comment == null)
                {
                    return NotFound();
                }

                var currentUserId = _userManager.GetUserId(User);
                if (comment.UserId != currentUserId && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }

                comment.Content = request.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении комментария {CommentId}", request.CommentId);
                return StatusCode(500);
            }
        }

        // POST: Applications/DeleteComment
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteComment([FromBody] DeleteCommentRequest request)
        {
            try
            {
                var comment = await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == request.CommentId);

                if (comment == null)
                {
                    return NotFound();
                }

                var currentUserId = _userManager.GetUserId(User);
                if (comment.UserId != currentUserId && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении комментария {CommentId}", request.CommentId);
                return StatusCode(500);
            }
        }
    }

    public class UpdateCommentRequest
    {
        public int CommentId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class DeleteCommentRequest
    {
        public int CommentId { get; set; }
    }
}