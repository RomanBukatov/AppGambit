using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;

namespace AppGambit.Controllers
{
    /// <summary>
    /// Контроллер для управления кэшированием и оптимизации производительности
    /// </summary>
    public class CacheController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CacheController> _logger;

        public CacheController(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<CacheController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Получение популярных приложений с кэшированием
        /// </summary>
        [ResponseCache(CacheProfileName = "Default")]
        public async Task<IActionResult> GetPopularApps(int count = 10)
        {
            const string cacheKey = "popular_apps";
            
            if (!_cache.TryGetValue(cacheKey, out List<Application> popularApps))
            {
                popularApps = await _context.Applications
                    .Include(a => a.User)
                    .Include(a => a.Ratings)
                    .OrderByDescending(a => a.Ratings.Average(r => (double?)r.Value) ?? 0)
                    .ThenByDescending(a => a.DownloadCount)
                    .Take(count)
                    .AsNoTracking()
                    .ToListAsync();

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    Priority = CacheItemPriority.High,
                    Size = 1
                };

                _cache.Set(cacheKey, popularApps, cacheOptions);
                _logger.LogInformation("Популярные приложения загружены в кэш");
            }

            return Json(popularApps);
        }

        /// <summary>
        /// Получение статистики с кэшированием
        /// </summary>
        [ResponseCache(CacheProfileName = "Default")]
        public async Task<IActionResult> GetStats()
        {
            const string cacheKey = "site_stats";
            
            if (!_cache.TryGetValue(cacheKey, out object stats))
            {
                var totalApps = await _context.Applications.CountAsync();
                var totalUsers = await _context.Users.CountAsync();
                var totalDownloads = await _context.Applications.SumAsync(a => a.DownloadCount);

                stats = new
                {
                    TotalApps = totalApps,
                    TotalUsers = totalUsers,
                    TotalDownloads = totalDownloads,
                    LastUpdated = DateTime.UtcNow
                };

                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                    Priority = CacheItemPriority.Normal,
                    Size = 1
                };

                _cache.Set(cacheKey, stats, cacheOptions);
                _logger.LogInformation("Статистика сайта загружена в кэш");
            }

            return Json(stats);
        }

        /// <summary>
        /// Предзагрузка критических данных
        /// </summary>
        public async Task<IActionResult> PreloadCriticalData()
        {
            try
            {
                // Предзагружаем популярные приложения
                await GetPopularApps();
                
                // Предзагружаем статистику
                await GetStats();
                
                // Предзагружаем категории
                const string categoryCacheKey = "app_categories";
                if (!_cache.TryGetValue(categoryCacheKey, out _))
                {
                    var categories = await _context.Applications
                        .Where(a => !string.IsNullOrEmpty(a.Category))
                        .GroupBy(a => a.Category)
                        .Select(g => new { Category = g.Key, Count = g.Count() })
                        .OrderByDescending(x => x.Count)
                        .Take(10)
                        .AsNoTracking()
                        .ToListAsync();

                    var categoryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
                        Size = 1
                    };
                    _cache.Set(categoryCacheKey, categories, categoryOptions);
                }

                return Json(new { success = true, message = "Критические данные предзагружены" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при предзагрузке критических данных");
                return Json(new { success = false, message = "Ошибка предзагрузки" });
            }
        }

        /// <summary>
        /// Очистка кэша (для администраторов)
        /// </summary>
        [HttpPost]
        public IActionResult ClearCache(string cacheKey = null)
        {
            try
            {
                if (string.IsNullOrEmpty(cacheKey))
                {
                    // Очищаем весь кэш (осторожно!)
                    if (_cache is MemoryCache memCache)
                    {
                        memCache.Compact(1.0);
                    }
                    _logger.LogInformation("Весь кэш очищен");
                }
                else
                {
                    _cache.Remove(cacheKey);
                    _logger.LogInformation($"Кэш с ключом {cacheKey} очищен");
                }

                return Json(new { success = true, message = "Кэш очищен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке кэша");
                return Json(new { success = false, message = "Ошибка очистки кэша" });
            }
        }

        /// <summary>
        /// Получение информации о состоянии кэша
        /// </summary>
        public IActionResult GetCacheInfo()
        {
            var cacheInfo = new
            {
                HasPopularApps = _cache.TryGetValue("popular_apps", out _),
                HasStats = _cache.TryGetValue("site_stats", out _),
                HasCategories = _cache.TryGetValue("app_categories", out _),
                Timestamp = DateTime.UtcNow
            };

            return Json(cacheInfo);
        }
    }
}