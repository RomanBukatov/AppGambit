using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _environment;

        public ApplicationsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IFileService fileService,
            IImageService imageService,
            ILogger<ApplicationsController> logger,
            IMemoryCache cache,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _fileService = fileService;
            _imageService = imageService;
            _logger = logger;
            _cache = cache;
            _environment = environment;
        }

        // GET: Applications/Search - глобальный поиск
        public async Task<IActionResult> Search(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return RedirectToAction(nameof(Index));
            }

            search = search.Trim();

            // Сначала ищем точное совпадение по DisplayName пользователя
            var exactUser = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.DisplayName != null && u.DisplayName.ToLower() == search.ToLower());
            
            if (exactUser != null)
            {
                return RedirectToAction("ByDisplayName", "Profile", new { displayName = exactUser.DisplayName });
            }

            // Затем ищем частичное совпадение по DisplayName или Email
            var partialUser = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u =>
                    (u.DisplayName != null && u.DisplayName.ToLower().Contains(search.ToLower())) ||
                    (u.Email != null && u.Email.ToLower().Contains(search.ToLower())));
            
            if (partialUser != null)
            {
                return RedirectToAction("ByDisplayName", "Profile", new { displayName = partialUser.DisplayName });
            }

            // Если пользователь не найден, ищем среди приложений
            return RedirectToAction(nameof(Index), new { search = search });
        }

        // GET: Applications - оптимизированная версия
        [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "search", "category", "page" })]
        public async Task<IActionResult> Index(string search, string category, int page = 1, int pageSize = 12)
        {
            var cacheKey = $"applications_list_{search}_{category}_{page}_{pageSize}";
            
            var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.SlidingExpiration = TimeSpan.FromMinutes(2);
                entry.Size = 1;
                
                // Оптимизированный запрос с минимальными включениями
                var query = _context.Applications.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();
                    query = query.Where(a =>
                        a.Name.Contains(search) ||
                        a.Description.Contains(search) ||
                        (a.DetailedDescription != null && a.DetailedDescription.Contains(search)) ||
                        (a.Category != null && a.Category.Contains(search)));
                }

                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(a => a.Category == category);
                }

                var totalItems = await query.CountAsync();
                
                // Получаем только необходимые данные одним запросом
                var applications = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Join(_context.Users, a => a.UserId, u => u.Id, (a, u) => new { App = a, User = u })
                    .Select(x => new ApplicationListItemViewModel
                    {
                        Id = x.App.Id,
                        Name = x.App.Name,
                        Description = x.App.Description,
                        IconUrl = x.App.IconUrl,
                        CreatedAt = x.App.CreatedAt,
                        DownloadCount = x.App.DownloadCount,
                        Category = x.App.Category,
                        Tags = x.App.Tags,
                        UserDisplayName = x.User.DisplayName ?? x.User.Email ?? "Неизвестный",
                        // Рейтинги получаем отдельным запросом для лучшей производительности
                        AverageRating = 0,
                        TotalRatings = 0,
                        LikesCount = 0,
                        DislikesCount = 0
                    })
                    .ToListAsync();

                // Получаем рейтинги отдельно для найденных приложений
                if (applications.Any())
                {
                    var appIds = applications.Select(a => a.Id).ToList();
                    var ratings = await _context.Ratings
                        .Where(r => appIds.Contains(r.ApplicationId))
                        .GroupBy(r => r.ApplicationId)
                        .Select(g => new
                        {
                            ApplicationId = g.Key,
                            AverageRating = g.Average(r => r.Value),
                            TotalRatings = g.Count(),
                            LikesCount = g.Count(r => r.IsLike),
                            DislikesCount = g.Count(r => !r.IsLike)
                        })
                        .AsNoTracking()
                        .ToListAsync();

                    // Обновляем рейтинги в приложениях
                    foreach (var app in applications)
                    {
                        var rating = ratings.FirstOrDefault(r => r.ApplicationId == app.Id);
                        if (rating != null)
                        {
                            app.AverageRating = rating.AverageRating;
                            app.TotalRatings = rating.TotalRatings;
                            app.LikesCount = rating.LikesCount;
                            app.DislikesCount = rating.DislikesCount;
                        }
                    }
                }

                return new
                {
                    Applications = applications,
                    TotalItems = totalItems,
                    TotalPages = (int)Math.Ceiling((double)totalItems / pageSize)
                };
            });

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentCategory = category;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = result.TotalPages;
            ViewBag.PageSize = pageSize;

            return View(result.Applications);
        }

        // GET: Applications/DetailsByName/{name}
        [ResponseCache(Duration = 600, VaryByQueryKeys = new[] { "name" })]
        public async Task<IActionResult> DetailsByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NotFound();
            }

            // Декодируем имя из URL (заменяем дефисы на пробелы)
            var decodedName = name.Replace("-", " ");
            var cacheKey = $"application_by_name_{decodedName.ToLower()}";

            var application = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                
                return await _context.Applications
                    .Include(a => a.User)
                    .Include(a => a.Ratings)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Name.ToLower() == decodedName.ToLower());
            });

            if (application == null)
            {
                return NotFound();
            }

            // Получаем комментарии отдельно
            var comments = await _cache.GetOrCreateAsync($"application_comments_{application.Id}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                
                return await _context.Comments
                    .Include(c => c.User)
                    .Where(c => c.ApplicationId == application.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(50)
                    .AsNoTracking()
                    .ToListAsync();
            });

            application.Comments = comments;

            // Получаем рейтинг текущего пользователя
            Rating? userRating = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                userRating = await _context.Set<Rating>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ApplicationId == application.Id && r.UserId == userId);
                
                ViewBag.CurrentUserId = userId;
                ViewBag.IsAdmin = User.IsInRole("Admin");
            }

            ViewBag.UserRating = userRating;

            return View("Details", application);
        }

        // GET: Applications/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cacheKey = $"application_details_{id}";
            var application = await _cache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.SlidingExpiration = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                
                return await _context.Applications
                    .Include(a => a.User)
                    .Include(a => a.Ratings)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id);
            });

            if (application == null)
            {
                return NotFound();
            }

            // Получаем комментарии отдельно для лучшей производительности
            var comments = await _cache.GetOrCreateAsync($"application_comments_{id}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                entry.Size = 1;
                
                return await _context.Comments
                    .Include(c => c.User)
                    .Where(c => c.ApplicationId == id)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(50) // Ограничиваем количество комментариев
                    .AsNoTracking()
                    .ToListAsync();
            });

            application.Comments = comments;

            // Получаем рейтинг текущего пользователя
            Rating? userRating = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = _userManager.GetUserId(User);
                userRating = await _context.Ratings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ApplicationId == id && r.UserId == userId);
                
                ViewBag.CurrentUserId = userId;
                ViewBag.IsAdmin = User.IsInRole("Admin");
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
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddComment([FromBody] AddCommentRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new { success = false, message = "Комментарий не может быть пустым" });
                }

                var content = request.Content.Trim();
                if (content.Length > 1000)
                {
                    return BadRequest(new { success = false, message = "Комментарий не может быть длиннее 1000 символов" });
                }

                var userId = _userManager.GetUserId(User);
                var comment = new Comment
                {
                    ApplicationId = request.ApplicationId,
                    UserId = userId!,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                // Получаем данные пользователя для ответа
                var user = await _userManager.GetUserAsync(User);
                
                _logger.LogInformation("Комментарий успешно добавлен пользователем {UserId} для приложения {ApplicationId}", userId, request.ApplicationId);
                return Ok(new {
                    success = true,
                    message = "Комментарий успешно добавлен",
                    comment = new {
                        id = comment.Id,
                        content = comment.Content,
                        createdAt = comment.CreatedAt,
                        user = new {
                            displayName = user?.DisplayName ?? user?.UserName ?? "Пользователь",
                            profileImageUrl = user?.ProfileImageUrl,
                            id = userId
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении комментария для приложения {ApplicationId}", request.ApplicationId);
                return StatusCode(500, new { success = false, message = "Произошла ошибка при добавлении комментария" });
            }
        }

        // POST: Applications/UpdateComment
        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
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
        [IgnoreAntiforgeryToken]
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
                
                _logger.LogInformation("Комментарий {CommentId} успешно удален пользователем {UserId}", request.CommentId, currentUserId);
                return Ok(new { success = true, message = "Комментарий успешно удален" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении комментария {CommentId}", request.CommentId);
                return StatusCode(500, new { success = false, message = "Внутренняя ошибка сервера при удалении комментария" });
            }
        }

        // GET: Applications/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var application = await _context.Applications
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (application.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var viewModel = new EditApplicationViewModel
            {
                Id = application.Id,
                Name = application.Name,
                Description = application.Description,
                DetailedDescription = application.DetailedDescription,
                Version = application.Version,
                Category = application.Category,
                TagsString = string.Join(",", application.Tags),
                CurrentIconUrl = application.IconUrl,
                CurrentDownloadUrl = application.DownloadUrl,
                CurrentScreenshots = application.Screenshots,
                CurrentTags = application.Tags
            };

            return View(viewModel);
        }

        // POST: Applications/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, EditApplicationViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var application = await _context.Applications.FindAsync(id);
            if (application == null)
            {
                return NotFound();
            }

            var currentUserId = _userManager.GetUserId(User);
            if (application.UserId != currentUserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                // Восстанавливаем текущие данные для отображения
                model.CurrentIconUrl = application.IconUrl;
                model.CurrentDownloadUrl = application.DownloadUrl;
                model.CurrentScreenshots = application.Screenshots;
                model.CurrentTags = application.Tags;
                return View(model);
            }

            try
            {
                // Обновляем основные поля
                application.Name = model.Name;
                application.Description = model.Description;
                application.DetailedDescription = model.DetailedDescription;
                application.Version = model.Version;
                application.Category = model.Category;
                application.UpdatedAt = DateTime.UtcNow;

                // Обработка тегов
                if (!string.IsNullOrEmpty(model.TagsString))
                {
                    application.Tags = model.TagsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList();
                }
                else
                {
                    application.Tags = new List<string>();
                }

                // Обновление иконки
                if (model.IconFile != null && _imageService.IsValidImageType(model.IconFile))
                {
                    // Удаляем старую иконку
                    if (!string.IsNullOrEmpty(application.IconUrl))
                    {
                        var oldIconPath = Path.Combine(_environment.WebRootPath, application.IconUrl);
                        if (System.IO.File.Exists(oldIconPath))
                        {
                            System.IO.File.Delete(oldIconPath);
                        }
                    }

                    application.IconUrl = await _imageService.SaveImageAsync(model.IconFile, "icons", 128, 128);
                }

                // Обновление файла приложения
                if (model.AppFile != null)
                {
                    var allowedExtensions = new[] { ".exe", ".msi", ".zip", ".rar", ".7z", ".apk" };
                    if (_fileService.IsValidFileType(model.AppFile, allowedExtensions))
                    {
                        // Удаляем старый файл
                        if (!string.IsNullOrEmpty(application.DownloadUrl))
                        {
                            var oldFilePath = Path.Combine(_environment.WebRootPath, application.DownloadUrl);
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                        }

                        application.DownloadUrl = await _fileService.SaveFileAsync(model.AppFile, "apps");
                        application.FileSize = model.AppFile.Length;
                    }
                    else
                    {
                        ModelState.AddModelError("AppFile", "Недопустимый тип файла приложения");
                        model.CurrentIconUrl = application.IconUrl;
                        model.CurrentDownloadUrl = application.DownloadUrl;
                        model.CurrentScreenshots = application.Screenshots;
                        model.CurrentTags = application.Tags;
                        return View(model);
                    }
                }

                // Удаление выбранных скриншотов
                if (model.ScreenshotsToDelete != null && model.ScreenshotsToDelete.Any())
                {
                    foreach (var screenshotToDelete in model.ScreenshotsToDelete)
                    {
                        application.Screenshots.Remove(screenshotToDelete);
                        var screenshotPath = Path.Combine(_environment.WebRootPath, screenshotToDelete);
                        if (System.IO.File.Exists(screenshotPath))
                        {
                            System.IO.File.Delete(screenshotPath);
                        }
                    }
                }

                // Добавление новых скриншотов
                if (model.Screenshots != null && model.Screenshots.Any())
                {
                    foreach (var screenshot in model.Screenshots.Where(s => s.Length > 0))
                    {
                        if (_imageService.IsValidImageType(screenshot))
                        {
                            var url = await _imageService.SaveImageAsync(screenshot, "screenshots");
                            application.Screenshots.Add(url);
                        }
                    }
                }

                _context.Update(application);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Приложение {AppId} успешно обновлено пользователем {UserId}", application.Id, currentUserId);
                return RedirectToAction(nameof(Details), new { id = application.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении приложения {AppId}", id);
                ModelState.AddModelError("", "Произошла ошибка при обновлении приложения");
                
                // Восстанавливаем текущие данные для отображения
                model.CurrentIconUrl = application.IconUrl;
                model.CurrentDownloadUrl = application.DownloadUrl;
                model.CurrentScreenshots = application.Screenshots;
                model.CurrentTags = application.Tags;
                return View(model);
            }
        }
    }

    public class UpdateCommentRequest
    {
        public int CommentId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class AddCommentRequest
    {
        public int ApplicationId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class DeleteCommentRequest
    {
        public int CommentId { get; set; }
    }
}