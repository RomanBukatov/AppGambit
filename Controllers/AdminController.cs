using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;
using AppGambit.ViewModels;

namespace AppGambit.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Admin
        public async Task<IActionResult> Index()
        {
            var stats = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalApplications = await _context.Applications.CountAsync(),
                TotalComments = await _context.Comments.CountAsync(),
                TotalRatings = await _context.Ratings.CountAsync(),
                RecentApplications = await _context.Applications
                    .Include(a => a.User)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(5)
                    .Select(a => new ApplicationListItemViewModel
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Description = a.Description,
                        CreatedAt = a.CreatedAt,
                        UserDisplayName = a.User.DisplayName ?? a.User.Email ?? "Неизвестный"
                    })
                    .ToListAsync(),
                RecentComments = await _context.Comments
                    .Include(c => c.User)
                    .Include(c => c.Application)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(10)
                    .Select(c => new AdminCommentViewModel
                    {
                        Id = c.Id,
                        Content = c.Content,
                        CreatedAt = c.CreatedAt,
                        UserDisplayName = c.User.DisplayName ?? c.User.Email ?? "Неизвестный",
                        ApplicationName = c.Application.Name,
                        ApplicationId = c.ApplicationId
                    })
                    .ToListAsync()
            };

            return View(stats);
        }

        // GET: Admin/Applications
        public async Task<IActionResult> Applications(string search, int page = 1, int pageSize = 20)
        {
            var query = _context.Applications
                .Include(a => a.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(a => 
                    a.Name.Contains(search) || 
                    a.Description.Contains(search) ||
                    a.User.DisplayName!.Contains(search) ||
                    a.User.Email!.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var applications = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AdminApplicationViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    CreatedAt = a.CreatedAt,
                    DownloadCount = a.DownloadCount,
                    UserDisplayName = a.User.DisplayName ?? a.User.Email ?? "Неизвестный",
                    UserId = a.UserId,
                    CommentsCount = a.Comments.Count()
                })
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.PageSize = pageSize;

            return View(applications);
        }

        // GET: Admin/Comments
        public async Task<IActionResult> Comments(string search, int page = 1, int pageSize = 20)
        {
            var query = _context.Comments
                .Include(c => c.User)
                .Include(c => c.Application)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(c => 
                    c.Content.Contains(search) || 
                    c.User.DisplayName!.Contains(search) ||
                    c.User.Email!.Contains(search) ||
                    c.Application.Name.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var comments = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new AdminCommentViewModel
                {
                    Id = c.Id,
                    Content = c.Content,
                    CreatedAt = c.CreatedAt,
                    UserDisplayName = c.User.DisplayName ?? c.User.Email ?? "Неизвестный",
                    UserId = c.UserId,
                    ApplicationName = c.Application.Name,
                    ApplicationId = c.ApplicationId
                })
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.PageSize = pageSize;

            return View(comments);
        }

        // POST: Admin/DeleteApplication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteApplication(int id)
        {
            try
            {
                var application = await _context.Applications
                    .Include(a => a.Comments)
                    .Include(a => a.Ratings)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (application == null)
                {
                    return NotFound();
                }

                var currentUserId = _userManager.GetUserId(User);
                _logger.LogWarning("Администратор {AdminId} удаляет приложение {AppId} '{AppName}' пользователя {UserId}", 
                    currentUserId, application.Id, application.Name, application.UserId);

                // Удаляем связанные файлы
                if (!string.IsNullOrEmpty(application.IconUrl))
                {
                    var iconPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", application.IconUrl.TrimStart('/'));
                    if (System.IO.File.Exists(iconPath))
                    {
                        System.IO.File.Delete(iconPath);
                    }
                }

                if (!string.IsNullOrEmpty(application.DownloadUrl))
                {
                    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", application.DownloadUrl.TrimStart('/'));
                    if (System.IO.File.Exists(filePath))
                    {
                        System.IO.File.Delete(filePath);
                    }
                }

                // Удаляем скриншоты
                if (application.Screenshots != null)
                {
                    foreach (var screenshot in application.Screenshots)
                    {
                        var screenshotPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", screenshot.TrimStart('/'));
                        if (System.IO.File.Exists(screenshotPath))
                        {
                            System.IO.File.Delete(screenshotPath);
                        }
                    }
                }

                _context.Applications.Remove(application);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Приложение '{application.Name}' успешно удалено.";
                return RedirectToAction(nameof(Applications));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении приложения {AppId}", id);
                TempData["ErrorMessage"] = "Произошла ошибка при удалении приложения.";
                return RedirectToAction(nameof(Applications));
            }
        }

        // POST: Admin/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int id)
        {
            try
            {
                var comment = await _context.Comments
                    .Include(c => c.User)
                    .Include(c => c.Application)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (comment == null)
                {
                    return NotFound();
                }

                var currentUserId = _userManager.GetUserId(User);
                _logger.LogWarning("Администратор {AdminId} удаляет комментарий {CommentId} пользователя {UserId} к приложению '{AppName}'", 
                    currentUserId, comment.Id, comment.UserId, comment.Application.Name);

                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Комментарий успешно удален.";
                return RedirectToAction(nameof(Comments));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении комментария {CommentId}", id);
                TempData["ErrorMessage"] = "Произошла ошибка при удалении комментария.";
                return RedirectToAction(nameof(Comments));
            }
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users(string search, int page = 1, int pageSize = 20)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u => 
                    u.DisplayName!.Contains(search) || 
                    u.Email!.Contains(search) ||
                    u.UserName!.Contains(search));
            }

            var totalItems = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserViewModel
                {
                    Id = u.Id,
                    DisplayName = u.DisplayName ?? "Не указано",
                    Email = u.Email ?? "Не указано",
                    UserName = u.UserName ?? "Не указано",
                    CreatedAt = u.CreatedAt,
                    ApplicationsCount = u.Applications.Count(),
                    CommentsCount = u.Comments.Count()
                })
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.PageSize = pageSize;

            return View(users);
        }
    }
}