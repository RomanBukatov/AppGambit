using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.ViewModels;

namespace AppGambit.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SearchController> _logger;

        public SearchController(ApplicationDbContext context, ILogger<SearchController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSearchSuggestions(string query, int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new { suggestions = new List<object>() });
            }

            try
            {
                query = query.Trim().ToLower();

                // Поиск приложений
                var appSuggestions = await _context.Applications
                    .Where(a => a.Name.ToLower().Contains(query) ||
                               a.Description.ToLower().Contains(query) ||
                               (a.Tags.Any() && a.Tags.Any(t => t.ToLower().Contains(query))))
                    .Select(a => new
                    {
                        type = "application",
                        id = a.Id,
                        title = a.Name,
                        description = a.Description.Length > 100 ? a.Description.Substring(0, 100) + "..." : a.Description,
                        iconUrl = a.IconImageId.HasValue ? $"/api/image/{a.IconImageId}" : null,
                        category = a.Category,
                        downloadCount = a.DownloadCount,
                        url = $"/Applications/Details/{a.Id}"
                    })
                    .Take(limit / 2)
                    .ToListAsync();

                // Поиск пользователей
                var userSuggestions = await _context.Users
                    .Where(u => u.DisplayName != null && u.DisplayName.ToLower().Contains(query))
                    .Select(u => new
                    {
                        type = "user",
                        id = u.Id,
                        title = u.DisplayName ?? u.Email ?? "Неизвестный",
                        description = $"Приложений: {u.Applications.Count()}",
                        iconUrl = u.ProfileImageId.HasValue ? $"/api/image/{u.ProfileImageId}" : null,
                        url = $"/{u.DisplayName}"
                    })
                    .Take(limit / 2)
                    .ToListAsync();

                // Поиск категорий
                var categorySuggestions = await _context.Applications
                    .Where(a => !string.IsNullOrEmpty(a.Category) && a.Category.ToLower().Contains(query))
                    .GroupBy(a => a.Category)
                    .Select(g => new
                    {
                        type = "category",
                        id = g.Key,
                        title = g.Key,
                        description = $"Приложений: {g.Count()}",
                        iconUrl = (string?)null,
                        url = $"/Applications?category={Uri.EscapeDataString(g.Key)}"
                    })
                    .Take(3)
                    .ToListAsync();

                var allSuggestions = new List<object>();
                allSuggestions.AddRange(appSuggestions);
                allSuggestions.AddRange(userSuggestions);
                allSuggestions.AddRange(categorySuggestions);

                return Ok(new { suggestions = allSuggestions.Take(limit) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении предложений поиска для запроса: {Query}", query);
                return Ok(new { suggestions = new List<object>() });
            }
        }

        [HttpGet("quick")]
        public async Task<IActionResult> QuickSearch(string query, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Ok(new { results = new List<object>(), total = 0 });
            }

            try
            {
                query = query.Trim();

                var applications = await _context.Applications
                    .Include(a => a.User)
                    .Where(a => a.Name.Contains(query) ||
                               a.Description.Contains(query) ||
                               a.Tags.Any(t => t.Contains(query)) ||
                               (a.Category != null && a.Category.Contains(query)))
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(limit)
                    .Select(a => new ApplicationListItemViewModel
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Description = a.Description,
                        Category = a.Category,
                        Tags = a.Tags.ToList(),
                        IconUrl = a.IconImageId.HasValue ? $"/api/image/{a.IconImageId}" : null,
                        DownloadCount = a.DownloadCount,
                        CreatedAt = a.CreatedAt,
                        UserDisplayName = a.User.DisplayName ?? a.User.Email ?? "Неизвестный",
                        AverageRating = a.Ratings.Any() ? a.Ratings.Average(r => r.Value) : 0,
                        TotalRatings = a.Ratings.Count()
                    })
                    .ToListAsync();

                var total = await _context.Applications
                    .Where(a => a.Name.Contains(query) ||
                               a.Description.Contains(query) ||
                               a.Tags.Any(t => t.Contains(query)) ||
                               (a.Category != null && a.Category.Contains(query)))
                    .CountAsync();

                return Ok(new { results = applications, total });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при быстром поиске для запроса: {Query}", query);
                return BadRequest(new { error = "Ошибка поиска" });
            }
        }

        [HttpGet("filters")]
        public async Task<IActionResult> GetSearchFilters()
        {
            try
            {
                var categories = await _context.Applications
                    .Where(a => !string.IsNullOrEmpty(a.Category))
                    .GroupBy(a => a.Category)
                    .Select(g => new { name = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToListAsync();

                var popularTags = await _context.Applications
                    .Where(a => a.Tags.Any())
                    .SelectMany(a => a.Tags)
                    .GroupBy(tag => tag.Trim().ToLower())
                    .Where(g => !string.IsNullOrEmpty(g.Key))
                    .Select(g => new { name = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .Take(20)
                    .ToListAsync();

                return Ok(new { categories, tags = popularTags });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении фильтров поиска");
                return BadRequest(new { error = "Ошибка получения фильтров" });
            }
        }
    }
}