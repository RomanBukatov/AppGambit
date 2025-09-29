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
        private readonly IDatabaseImageService _databaseImageService;
        private readonly IDatabaseFileService _databaseFileService;
        private readonly ILogger<ApplicationsController> _logger;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _environment;

        public ApplicationsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IFileService fileService,
            IImageService imageService,
            IDatabaseImageService databaseImageService,
            IDatabaseFileService databaseFileService,
            ILogger<ApplicationsController> logger,
            IMemoryCache cache,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _fileService = fileService;
            _imageService = imageService;
            _databaseImageService = databaseImageService;
            _databaseFileService = databaseFileService;
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

                // Фильтруем по категории на уровне базы данных
                if (!string.IsNullOrEmpty(category))
                {
                    query = query.Where(a => a.Category == category);
                }

                // Получаем все приложения (с учетом категории, если указана)
                var allApplications = await query.ToListAsync();
                
                // Если есть поиск, фильтруем по тексту ИЛИ тегам в памяти
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();
                    allApplications = allApplications.Where(a =>
                        // Поиск по тексту
                        a.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        a.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (a.DetailedDescription != null && a.DetailedDescription.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                        (a.Category != null && a.Category.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                        // Поиск по тегам
                        (a.Tags != null && a.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    ).ToList();
                }
                
                var totalItems = allApplications.Count;
                
                // Применяем сортировку и пагинацию
                var paginatedApplicationIds = allApplications
                    .OrderByDescending(a => a.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(a => a.Id)
                    .ToList();
                
                // Получаем только необходимые данные одним запросом для отфильтрованных приложений
                var applications = await _context.Applications
                    .AsNoTracking()
                    .Where(a => paginatedApplicationIds.Contains(a.Id))
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
                
                // Сортируем результаты в соответствии с порядком ID из paginatedApplicationIds
                applications = applications
                    .OrderBy(a => paginatedApplicationIds.IndexOf(a.Id))
                    .ToList();

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
            // Временно отключаем кэш для диагностики проблемы
            _logger.LogInformation("🔍 Загружаем комментарии для приложения {ApplicationId} (DetailsByName)", application.Id);
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.ApplicationId == application.Id)
                .OrderByDescending(c => c.CreatedAt)
                .Take(50)
                .AsNoTracking()
                .ToListAsync();
            
            _logger.LogInformation("📊 Найдено комментариев: {CommentsCount} (DetailsByName)", comments.Count);

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
            // Временно отключаем кэш для диагностики проблемы
            _logger.LogInformation("🔍 Загружаем комментарии для приложения {ApplicationId}", id);
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.ApplicationId == id)
                .OrderByDescending(c => c.CreatedAt)
                .Take(50) // Ограничиваем количество комментариев
                .AsNoTracking()
                .ToListAsync();
            
            _logger.LogInformation("📊 Найдено комментариев: {CommentsCount}", comments.Count);
            foreach (var comment in comments)
            {
                _logger.LogInformation("💬 Комментарий ID: {CommentId}, Автор: {UserId}, Содержимое: {Content}",
                    comment.Id, comment.UserId, comment.Content.Substring(0, Math.Min(50, comment.Content.Length)));
            }

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

                // Сначала сохраняем приложение, чтобы получить ID
                _logger.LogInformation("Добавление приложения в контекст БД: {AppName}", application.Name);
                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                // Теперь сохраняем иконку в БД с ID приложения
                if (model.IconFile != null && _databaseImageService.IsValidImageType(model.IconFile))
                {
                    _logger.LogInformation("Сохранение иконки для приложения {AppName} в БД", application.Name);
                    var iconImage = await _databaseImageService.SaveImageAsync(
                        model.IconFile,
                        ImageType.ApplicationIcon,
                        application.Id,
                        maxWidth: 128,
                        maxHeight: 128);
                    application.IconImageId = iconImage.Id;
                    
                    // Сохраняем также старый URL для совместимости
                    application.IconUrl = $"/Image/{iconImage.Id}";
                }

                // Сохранение файла приложения в БД - обязательное поле
                if (model.AppFile == null)
                {
                    ModelState.AddModelError("AppFile", "Файл приложения обязателен");
                    return View(model);
                }

                if (_databaseFileService.IsValidFileType(model.AppFile))
                {
                    _logger.LogInformation("Сохранение файла приложения {AppName} в БД", application.Name);
                    var appFile = await _databaseFileService.SaveFileAsync(
                        model.AppFile,
                        ImageType.ApplicationFile,
                        application.Id);
                    application.AppFileId = appFile.Id;
                    application.FileSize = model.AppFile.Length;

                    // Сохраняем также старый URL для совместимости (теперь указывает на БД)
                    application.DownloadUrl = $"/Applications/DownloadFile/{appFile.Id}";
                }
                else
                {
                    ModelState.AddModelError("AppFile", "Недопустимый тип файла приложения");
                    return View(model);
                }

                // Сохранение скриншотов в БД
                if (model.Screenshots != null && model.Screenshots.Any())
                {
                    var screenshotUrls = new List<string>();
                    
                    foreach (var screenshot in model.Screenshots.Where(s => s.Length > 0))
                    {
                        if (_databaseImageService.IsValidImageType(screenshot))
                        {
                            _logger.LogInformation("Сохранение скриншота для приложения {AppName} в БД", application.Name);
                            var screenshotImage = await _databaseImageService.SaveImageAsync(
                                screenshot,
                                ImageType.ApplicationScreenshot,
                                application.Id);
                            
                            screenshotUrls.Add($"/Image/{screenshotImage.Id}");
                        }
                    }
                    
                    application.Screenshots = screenshotUrls;
                }

                _logger.LogInformation("Сохранение окончательных изменений в БД для приложения: {AppName}", application.Name);
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

        // GET: Applications/Download
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var application = await _context.Applications
                .FirstOrDefaultAsync(a => a.Id == id);
            
            if (application == null)
            {
                return NotFound();
            }

            // Увеличиваем счетчик скачиваний
            application.DownloadCount++;
            
            // Явно помечаем сущность как изменённую
            _context.Entry(application).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            
            // Очищаем все связанные кэши для этого приложения
            _cache.Remove($"application_details_{id}");
            _cache.Remove($"application_by_name_{application.Name.ToLower()}");
            _cache.Remove($"application_by_name_{application.Name.ToLower().Replace(" ", "-")}");
            _cache.Remove($"application_comments_{id}");
            
            // Также очищаем кэш списка приложений
            _cache.Remove("applications_index");
            
            // Очищаем кэш профиля пользователя
            if (application.UserId != null)
            {
                _cache.Remove($"user_profile_{application.UserId}");
            }

            // Проверяем наличие файла в БД
            if (application.AppFileId == null)
            {
                return NotFound("Файл для скачивания не указан");
            }

            var fileData = await _databaseFileService.GetFileDataAsync(application.AppFileId.Value);
            if (fileData == null || fileData.Length == 0)
            {
                return NotFound("Файл не найден");
            }

            var fileName = await _databaseFileService.GetFileNameAsync(application.AppFileId.Value);
            var contentType = await _databaseFileService.GetFileContentTypeAsync(application.AppFileId.Value);
            
            _logger.LogInformation("Скачивание файла: {FileName} для приложения {AppId}", fileName, id);
            return File(fileData, contentType, fileName);
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
            _logger.LogInformation("🚀 Начало обработки запроса на добавление комментария");
            _logger.LogInformation("📝 Данные запроса: ApplicationId={ApplicationId}, Content='{Content}' (длина: {ContentLength})",
                request.ApplicationId, request.Content, request.Content?.Length ?? 0);
            
            // Проверяем подключение к базе данных
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation("🔌 Подключение к БД: {CanConnect}", canConnect);
                
                if (!canConnect)
                {
                    _logger.LogError("❌ Не удается подключиться к базе данных");
                    return StatusCode(500, new { success = false, message = "Ошибка подключения к базе данных" });
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "💥 Ошибка при проверке подключения к БД");
                return StatusCode(500, new { success = false, message = "Ошибка подключения к базе данных" });
            }
            
            try
            {
                // Проверяем аутентификацию пользователя
                if (!User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("❌ Пользователь не аутентифицирован");
                    return Unauthorized(new { success = false, message = "Необходима авторизация" });
                }

                var userId = _userManager.GetUserId(User);
                _logger.LogInformation("👤 ID пользователя: {UserId}", userId);

                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    _logger.LogWarning("❌ Пустой комментарий от пользователя {UserId}", userId);
                    return BadRequest(new { success = false, message = "Комментарий не может быть пустым" });
                }

                var content = request.Content.Trim();
                if (content.Length > 1000)
                {
                    _logger.LogWarning("❌ Слишком длинный комментарий от пользователя {UserId}: {Length} символов", userId, content.Length);
                    return BadRequest(new { success = false, message = "Комментарий не может быть длиннее 1000 символов" });
                }

                // Проверяем существование приложения
                var applicationExists = await _context.Applications.AnyAsync(a => a.Id == request.ApplicationId);
                if (!applicationExists)
                {
                    _logger.LogWarning("❌ Приложение с ID {ApplicationId} не найдено", request.ApplicationId);
                    return NotFound(new { success = false, message = "Приложение не найдено" });
                }

                _logger.LogInformation("✅ Валидация прошла успешно, создаем комментарий");

                var comment = new Comment
                {
                    ApplicationId = request.ApplicationId,
                    UserId = userId!,
                    Content = content,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("💾 Добавляем комментарий в контекст БД");
                _logger.LogInformation("🔧 Текущий режим отслеживания: {TrackingBehavior}", _context.ChangeTracker.QueryTrackingBehavior);
                
                _context.Comments.Add(comment);
                
                _logger.LogInformation("💾 Сохраняем изменения в БД");
                var savedChanges = await _context.SaveChangesAsync();
                _logger.LogInformation("💾 Количество сохраненных записей: {SavedChanges}", savedChanges);
                
                _logger.LogInformation("✅ Комментарий сохранен с ID: {CommentId}", comment.Id);
                
                // Проверяем, что комментарий действительно сохранился
                var savedComment = await _context.Comments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == comment.Id);
                
                if (savedComment != null)
                {
                    _logger.LogInformation("✅ Подтверждение: комментарий найден в БД с ID: {CommentId}", savedComment.Id);
                }
                else
                {
                    _logger.LogError("❌ Комментарий не найден в БД после сохранения!");
                    return StatusCode(500, new { success = false, message = "Ошибка сохранения комментария" });
                }

                // Очищаем кэш комментариев для этого приложения
                _logger.LogInformation("🗑️ Очищаем кэш комментариев для приложения {ApplicationId}", request.ApplicationId);
                _cache.Remove($"application_comments_{request.ApplicationId}");
                _cache.Remove($"application_details_{request.ApplicationId}");
                
                // Получаем данные пользователя для ответа
                _logger.LogInformation("👤 Получаем данные пользователя для ответа");
                var user = await _userManager.GetUserAsync(User);
                
                var responseData = new {
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
                };

                _logger.LogInformation("📤 Отправляем успешный ответ: {@ResponseData}", responseData);
                _logger.LogInformation("✅ Комментарий успешно добавлен пользователем {UserId} для приложения {ApplicationId}", userId, request.ApplicationId);
                
                return Ok(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Критическая ошибка при добавлении комментария для приложения {ApplicationId}. Сообщение: {Message}",
                    request.ApplicationId, ex.Message);
                _logger.LogError("📍 Stack trace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { success = false, message = "Произошла ошибка при добавлении комментария" });
            }
        }

        // POST: Applications/UpdateComment
        [HttpPost]
        [Authorize]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateComment([FromBody] UpdateCommentRequest request)
        {
            _logger.LogInformation("🔄 Начало обработки запроса на обновление комментария");
            _logger.LogInformation("📝 Данные запроса: CommentId={CommentId}, Content='{Content}' (длина: {ContentLength})",
                request.CommentId, request.Content, request.Content?.Length ?? 0);
            
            try
            {
                // Проверяем аутентификацию пользователя
                if (!User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("❌ Пользователь не аутентифицирован");
                    return Unauthorized(new { success = false, message = "Необходима авторизация" });
                }

                var currentUserId = _userManager.GetUserId(User);
                _logger.LogInformation("👤 ID пользователя: {UserId}", currentUserId);

                if (string.IsNullOrWhiteSpace(request.Content))
                {
                    _logger.LogWarning("❌ Пустой комментарий от пользователя {UserId}", currentUserId);
                    return BadRequest(new { success = false, message = "Комментарий не может быть пустым" });
                }

                if (request.Content.Trim().Length > 1000)
                {
                    _logger.LogWarning("❌ Слишком длинный комментарий от пользователя {UserId}: {Length} символов", currentUserId, request.Content.Trim().Length);
                    return BadRequest(new { success = false, message = "Комментарий не может быть длиннее 1000 символов" });
                }

                _logger.LogInformation("🔍 Ищем комментарий с ID: {CommentId}", request.CommentId);
                
                // Включаем отслеживание для операции обновления
                var comment = await _context.Comments
                    .AsTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.CommentId);

                if (comment == null)
                {
                    _logger.LogWarning("❌ Комментарий с ID {CommentId} не найден", request.CommentId);
                    return NotFound(new { success = false, message = "Комментарий не найден" });
                }

                _logger.LogInformation("✅ Комментарий найден. Автор: {AuthorId}, Текущий пользователь: {CurrentUserId}", comment.UserId, currentUserId);

                if (comment.UserId != currentUserId)
                {
                    _logger.LogWarning("❌ Пользователь {UserId} пытается редактировать чужой комментарий {CommentId} (автор: {AuthorId})",
                        currentUserId, request.CommentId, comment.UserId);
                    return Forbid();
                }

                _logger.LogInformation("💾 Обновляем содержимое комментария с '{OldContent}' на '{NewContent}'",
                    comment.Content, request.Content.Trim());
                
                comment.Content = request.Content.Trim();
                comment.UpdatedAt = DateTime.UtcNow;

                // Явно помечаем сущность как измененную
                _context.Entry(comment).State = EntityState.Modified;

                _logger.LogInformation("💾 Сохраняем изменения в БД");
                var savedChanges = await _context.SaveChangesAsync();
                _logger.LogInformation("💾 Количество сохраненных записей: {SavedChanges}", savedChanges);
                
                // Очищаем кэш комментариев
                _logger.LogInformation("🗑️ Очищаем кэш комментариев для приложения {ApplicationId}", comment.ApplicationId);
                _cache.Remove($"application_comments_{comment.ApplicationId}");
                _cache.Remove($"application_details_{comment.ApplicationId}");
                
                // Проверяем, что изменения сохранились
                var updatedComment = await _context.Comments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.CommentId);
                
                if (updatedComment != null && updatedComment.Content == request.Content.Trim())
                {
                    _logger.LogInformation("✅ Подтверждение: комментарий обновлен в БД. Новое содержимое: '{Content}'", updatedComment.Content);
                }
                else
                {
                    _logger.LogError("❌ Комментарий не обновился в БД!");
                    return StatusCode(500, new { success = false, message = "Ошибка обновления комментария" });
                }
                
                _logger.LogInformation("✅ Комментарий {CommentId} успешно обновлен пользователем {UserId}", request.CommentId, currentUserId);
                return Ok(new { success = true, message = "Комментарий успешно обновлен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Критическая ошибка при обновлении комментария {CommentId}. Сообщение: {Message}",
                    request.CommentId, ex.Message);
                _logger.LogError("📍 Stack trace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { success = false, message = "Произошла ошибка при обновлении комментария" });
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
                if (comment.UserId != currentUserId)
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
            if (application.UserId != currentUserId)
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
            if (application.UserId != currentUserId)
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
                if (model.IconFile != null && _databaseImageService.IsValidImageType(model.IconFile))
                {
                    // Удаляем старую иконку из БД
                    if (application.IconImageId.HasValue)
                    {
                        await _databaseImageService.DeleteImageAsync(application.IconImageId.Value);
                    }

                    // Сохраняем новую иконку в БД
                    var iconImage = await _databaseImageService.SaveImageAsync(
                        model.IconFile,
                        ImageType.ApplicationIcon,
                        application.Id,
                        maxWidth: 128,
                        maxHeight: 128);
                    
                    application.IconImageId = iconImage.Id;
                    application.IconUrl = $"/Image/{iconImage.Id}";
                }

                // Обновление файла приложения в БД
                if (model.AppFile != null)
                {
                    if (_databaseFileService.IsValidFileType(model.AppFile))
                    {
                        // Удаляем старый файл из БД, если он есть
                        if (application.AppFileId.HasValue)
                        {
                            await _databaseFileService.DeleteFileAsync(application.AppFileId.Value);
                            _logger.LogInformation("Удален старый файл приложения с ID {FileId}", application.AppFileId.Value);
                        }

                        // Сохраняем новый файл в БД
                        _logger.LogInformation("Сохранение нового файла приложения {AppName} в БД", application.Name);
                        var appFile = await _databaseFileService.SaveFileAsync(
                            model.AppFile,
                            ImageType.ApplicationFile,
                            application.Id);
                        application.AppFileId = appFile.Id;
                        application.FileSize = model.AppFile.Length;
                        
                        // Обновляем URL для совместимости
                        application.DownloadUrl = $"/Applications/DownloadFile/{appFile.Id}";
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

                // Обработка нового порядка скриншотов
                if (!string.IsNullOrEmpty(model.ScreenshotsOrder))
                {
                    var orderedScreenshots = model.ScreenshotsOrder.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToList();
                    
                    // Проверяем, что все скриншоты из нового порядка существуют в текущих
                    var validOrderedScreenshots = orderedScreenshots
                        .Where(s => application.Screenshots.Contains(s))
                        .ToList();
                    
                    // Если есть изменения в порядке, применяем их
                    if (validOrderedScreenshots.Any())
                    {
                        application.Screenshots = validOrderedScreenshots;
                    }
                }

                // Удаление выбранных скриншотов
                if (model.ScreenshotsToDelete != null && model.ScreenshotsToDelete.Any())
                {
                    foreach (var screenshotToDelete in model.ScreenshotsToDelete)
                    {
                        application.Screenshots.Remove(screenshotToDelete);
                        
                        // Извлекаем ID изображения из URL и удаляем из БД
                        if (screenshotToDelete.StartsWith("/Image/"))
                        {
                            var imageIdStr = screenshotToDelete.Replace("/Image/", "");
                            if (int.TryParse(imageIdStr, out int imageId))
                            {
                                await _databaseImageService.DeleteImageAsync(imageId);
                            }
                        }
                    }
                }

                // Добавление новых скриншотов
                if (model.Screenshots != null && model.Screenshots.Any())
                {
                    foreach (var screenshot in model.Screenshots.Where(s => s.Length > 0))
                    {
                        if (_databaseImageService.IsValidImageType(screenshot))
                        {
                            var screenshotImage = await _databaseImageService.SaveImageAsync(
                                screenshot,
                                ImageType.ApplicationScreenshot,
                                application.Id);
                            
                            application.Screenshots.Add($"/Image/{screenshotImage.Id}");
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

        // GET: Applications/Delete/5
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var application = await _context.Applications
                .Include(a => a.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            // Проверяем, что пользователь является создателем приложения
            var currentUserId = _userManager.GetUserId(User);
            if (application.UserId != currentUserId)
            {
                return Forbid();
            }

            return View(application);
        }

        // POST: Applications/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                _logger.LogInformation("Начинаем удаление приложения {AppId}", id);

                var application = await _context.Applications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    _logger.LogWarning("Приложение {AppId} не найдено для удаления", id);
                    return NotFound();
                }

                // Проверяем, что пользователь является создателем приложения
                var currentUserId = _userManager.GetUserId(User);
                if (application.UserId != currentUserId)
                {
                    _logger.LogWarning("Пользователь {UserId} пытается удалить чужое приложение {AppId}", currentUserId, id);
                    return Forbid();
                }

                _logger.LogInformation("Приложение {AppId} найдено: {AppName}, пользователь: {UserId}", id, application.Name, currentUserId);

                // Шаг 1: Удаляем комментарии (может быть много, делаем отдельно)
                _logger.LogInformation("Шаг 1: Удаление комментариев для приложения {AppId}", id);
                var commentsDeleted = await _context.Comments
                    .Where(c => c.ApplicationId == id)
                    .ExecuteDeleteAsync();
                _logger.LogInformation("Удалено комментариев: {Count}", commentsDeleted);

                // Шаг 2: Удаляем рейтинги
                _logger.LogInformation("Шаг 2: Удаление рейтингов для приложения {AppId}", id);
                var ratingsDeleted = await _context.Ratings
                    .Where(r => r.ApplicationId == id)
                    .ExecuteDeleteAsync();
                _logger.LogInformation("Удалено рейтингов: {Count}", ratingsDeleted);

                // Шаг 3: Удаляем связанные файлы и изображения
                _logger.LogInformation("Шаг 3: Удаление связанных файлов и изображений");

                // Удаляем файл приложения
                if (application.AppFileId.HasValue)
                {
                    try
                    {
                        await _databaseFileService.DeleteFileAsync(application.AppFileId.Value);
                        _logger.LogInformation("Файл приложения {FileId} удален", application.AppFileId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при удалении файла приложения {FileId}", application.AppFileId.Value);
                    }
                }

                // Удаляем иконку
                if (application.IconImageId.HasValue)
                {
                    try
                    {
                        await _databaseImageService.DeleteImageAsync(application.IconImageId.Value);
                        _logger.LogInformation("Иконка приложения {IconId} удалена", application.IconImageId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при удалении иконки приложения {IconId}", application.IconImageId.Value);
                    }
                }

                // Удаляем скриншоты
                var screenshots = await _context.Images
                    .Where(i => i.ApplicationId == id && i.Type == ImageType.ApplicationScreenshot)
                    .ToListAsync();

                foreach (var screenshot in screenshots)
                {
                    try
                    {
                        await _databaseImageService.DeleteImageAsync(screenshot.Id);
                        _logger.LogInformation("Скриншот {ScreenshotId} удален", screenshot.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при удалении скриншота {ScreenshotId}", screenshot.Id);
                    }
                }

                // Шаг 4: Удаляем само приложение
                _logger.LogInformation("Шаг 4: Удаление приложения {AppId}", id);
                var appDeleted = await _context.Applications
                    .Where(a => a.Id == id)
                    .ExecuteDeleteAsync();
                _logger.LogInformation("Удалено приложений: {Count}", appDeleted);

                // Очищаем кэш
                _logger.LogInformation("Очистка кэша для приложения {AppId}", id);
                _cache.Remove($"application_details_{id}");
                _cache.Remove($"application_by_name_{application.Name.ToLower()}");
                _cache.Remove($"application_by_name_{application.Name.ToLower().Replace(" ", "-")}");
                _cache.Remove("applications_index");

                if (application.UserId != null)
                {
                    _cache.Remove($"user_profile_{application.UserId}");
                }

                _logger.LogInformation("✅ Приложение {AppId} '{AppName}' успешно удалено пользователем {UserId}",
                    id, application.Name, currentUserId);

                TempData["SuccessMessage"] = "Приложение успешно удалено";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Критическая ошибка при удалении приложения {AppId}", id);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);

                TempData["ErrorMessage"] = "Произошла ошибка при удалении приложения. Попробуйте еще раз.";
                return RedirectToAction(nameof(Details), new { id });
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