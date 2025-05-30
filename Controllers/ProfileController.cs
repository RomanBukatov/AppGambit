using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;
using AppGambit.Services;

namespace AppGambit.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IImageService _imageService;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IImageService imageService,
            ILogger<ProfileController> logger)
        {
            _context = context;
            _userManager = userManager;
            _imageService = imageService;
            _logger = logger;
        }

        // GET: Profile/ByDisplayName/{displayName}
        public async Task<IActionResult> ByDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return NotFound();
            }

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.DisplayName == displayName);

            if (user == null)
            {
                return NotFound();
            }

            return RedirectToAction("Details", new { id = user.Id });
        }

        // GET: Profile/Details/5 (по ID пользователя)
        public async Task<IActionResult> Details(int id)
        {
            // Поиск пользователя по индексу (можно использовать хеш от ID)
            var users = await _userManager.Users.ToListAsync();
            if (id <= 0 || id > users.Count)
            {
                return NotFound();
            }

            var user = users[id - 1]; // Индекс начинается с 1
            
            var userApplications = await _context.Applications
                .Include(a => a.Ratings)
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // Статистика профиля
            var totalApplications = userApplications.Count;
            var totalDownloads = userApplications.Sum(a => a.DownloadCount);
            var averageRating = userApplications.Any() && userApplications.SelectMany(a => a.Ratings).Any()
                ? userApplications.SelectMany(a => a.Ratings).Average(r => r.Value)
                : 0;
            var totalComments = await _context.Comments
                .Where(c => c.UserId == user.Id)
                .CountAsync();

            ViewBag.Applications = userApplications;
            ViewBag.UserIndex = id;
            ViewBag.IsOwnProfile = User.Identity?.IsAuthenticated == true &&
                                  _userManager.GetUserId(User) == user.Id;
            
            // Статистика
            ViewBag.TotalApplications = totalApplications;
            ViewBag.TotalDownloads = totalDownloads;
            ViewBag.AverageRating = Math.Round(averageRating, 1);
            ViewBag.TotalComments = totalComments;

            return View(user);
        }

        // GET: Profile/Edit
        [Authorize]
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(User model, IFormFile? profileImage)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            try
            {
                // Обновление отображаемого имени
                if (!string.IsNullOrWhiteSpace(model.DisplayName))
                {
                    user.DisplayName = model.DisplayName.Trim();
                }

                // Обновление email
                if (!string.IsNullOrWhiteSpace(model.Email) && model.Email != user.Email)
                {
                    var setEmailResult = await _userManager.SetEmailAsync(user, model.Email);
                    if (!setEmailResult.Succeeded)
                    {
                        foreach (var error in setEmailResult.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }
                }

                // Обновление изображения профиля
                if (profileImage != null && _imageService.IsValidImageType(profileImage))
                {
                    // Удаление старого изображения
                    if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        var oldImagePath = user.ProfileImageUrl;
                        // Асинхронное удаление в фоне
                        _ = Task.Run(async () => await _imageService.ConvertToWebPAsync(oldImagePath));
                    }

                    user.ProfileImageUrl = await _imageService.SaveImageAsync(profileImage, "profiles", 200, 200);
                }

                user.UpdatedAt = DateTime.UtcNow;

                var updateResult = await _userManager.UpdateAsync(user);
                if (updateResult.Succeeded)
                {
                    // Получение индекса пользователя для редиректа
                    var users = await _userManager.Users.ToListAsync();
                    var userIndex = users.FindIndex(u => u.Id == user.Id) + 1;
                    
                    return RedirectToAction(nameof(Details), new { id = userIndex });
                }

                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении профиля пользователя {UserId}", user.Id);
                ModelState.AddModelError("", "Произошла ошибка при обновлении профиля");
            }

            return View(model);
        }

        // GET: Profile/Search
        public async Task<IActionResult> Search(string? query, int page = 1)
        {
            const int pageSize = 20;
            
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                usersQuery = usersQuery.Where(u => 
                    u.DisplayName!.Contains(query) || 
                    u.UserName!.Contains(query) ||
                    u.Email!.Contains(query));
            }

            var totalUsers = await usersQuery.CountAsync();
            var users = await usersQuery
                .OrderBy(u => u.DisplayName ?? u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Создание списка с индексами
            var allUsers = await _userManager.Users.ToListAsync();
            var usersWithIndexes = users.Select(user => new
            {
                User = user,
                Index = allUsers.FindIndex(u => u.Id == user.Id) + 1
            }).ToList();

            ViewBag.UsersWithIndexes = usersWithIndexes;
            ViewBag.CurrentQuery = query;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);

            return View();
        }

        // GET: Profile/MyProfile
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            // Получение индекса текущего пользователя
            var users = await _userManager.Users.ToListAsync();
            var userIndex = users.FindIndex(u => u.Id == user.Id) + 1;

            return RedirectToAction(nameof(Details), new { id = userIndex });
        }
    }
}