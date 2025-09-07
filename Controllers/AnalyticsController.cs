using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.ViewModels;

namespace AppGambit.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(ApplicationDbContext context, ILogger<AnalyticsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var now = DateTime.UtcNow;
                var thirtyDaysAgo = now.AddDays(-30);
                var sevenDaysAgo = now.AddDays(-7);

                // Основная статистика
                var totalUsers = await _context.Users.CountAsync();
                var totalApplications = await _context.Applications.CountAsync();
                var totalComments = await _context.Comments.CountAsync();
                var totalDownloads = await _context.Applications.SumAsync(a => a.DownloadCount);

                // Статистика за последние 30 дней
                var newUsersLast30Days = await _context.Users
                    .Where(u => u.CreatedAt >= thirtyDaysAgo)
                    .CountAsync();

                var newAppsLast30Days = await _context.Applications
                    .Where(a => a.CreatedAt >= thirtyDaysAgo)
                    .CountAsync();

                var newCommentsLast30Days = await _context.Comments
                    .Where(c => c.CreatedAt >= thirtyDaysAgo)
                    .CountAsync();

                // Статистика за последние 7 дней
                var newUsersLast7Days = await _context.Users
                    .Where(u => u.CreatedAt >= sevenDaysAgo)
                    .CountAsync();

                var newAppsLast7Days = await _context.Applications
                    .Where(a => a.CreatedAt >= sevenDaysAgo)
                    .CountAsync();

                // Топ категории
                var topCategories = await _context.Applications
                    .Where(a => !string.IsNullOrEmpty(a.Category))
                    .GroupBy(a => a.Category)
                    .Select(g => new { category = g.Key, count = g.Count(), downloads = g.Sum(a => a.DownloadCount) })
                    .OrderByDescending(x => x.count)
                    .Take(10)
                    .ToListAsync();

                // Самые активные пользователи
                var topUsers = await _context.Users
                    .Select(u => new {
                        id = u.Id,
                        displayName = u.DisplayName ?? u.Email ?? "Неизвестный",
                        appsCount = u.Applications.Count(),
                        commentsCount = u.Comments.Count(),
                        totalDownloads = u.Applications.Sum(a => a.DownloadCount)
                    })
                    .Where(u => u.appsCount > 0 || u.commentsCount > 0)
                    .OrderByDescending(u => u.appsCount + u.commentsCount)
                    .Take(10)
                    .ToListAsync();

                // Самые популярные приложения
                var topApps = await _context.Applications
                    .Include(a => a.User)
                    .OrderByDescending(a => a.DownloadCount)
                    .Take(10)
                    .Select(a => new {
                        id = a.Id,
                        name = a.Name,
                        downloads = a.DownloadCount,
                        rating = a.Ratings.Any() ? a.Ratings.Average(r => r.Value) : 0,
                        author = a.User.DisplayName ?? a.User.Email ?? "Неизвестный",
                        category = a.Category
                    })
                    .ToListAsync();

                var result = new
                {
                    overview = new
                    {
                        totalUsers,
                        totalApplications,
                        totalComments,
                        totalDownloads,
                        newUsersLast30Days,
                        newAppsLast30Days,
                        newCommentsLast30Days,
                        newUsersLast7Days,
                        newAppsLast7Days
                    },
                    topCategories,
                    topUsers,
                    topApps
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении данных аналитики");
                return BadRequest(new { error = "Ошибка получения данных аналитики" });
            }
        }

        [HttpGet("charts/users-growth")]
        public async Task<IActionResult> GetUsersGrowthChart(int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days).Date;
                var endDate = DateTime.UtcNow.Date;

                var userGrowth = await _context.Users
                    .Where(u => u.CreatedAt >= startDate)
                    .GroupBy(u => u.CreatedAt.Date)
                    .Select(g => new { date = g.Key, count = g.Count() })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                // Заполняем пропущенные дни нулями
                var result = new List<object>();
                var currentDate = startDate;
                var cumulativeCount = await _context.Users.Where(u => u.CreatedAt < startDate).CountAsync();

                while (currentDate <= endDate)
                {
                    var dayData = userGrowth.FirstOrDefault(x => x.date == currentDate);
                    var dailyCount = dayData?.count ?? 0;
                    cumulativeCount += dailyCount;

                    result.Add(new
                    {
                        date = currentDate.ToString("yyyy-MM-dd"),
                        dailyCount,
                        totalCount = cumulativeCount
                    });

                    currentDate = currentDate.AddDays(1);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении графика роста пользователей");
                return BadRequest(new { error = "Ошибка получения данных графика" });
            }
        }

        [HttpGet("charts/apps-growth")]
        public async Task<IActionResult> GetAppsGrowthChart(int days = 30)
        {
            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days).Date;
                var endDate = DateTime.UtcNow.Date;

                var appGrowth = await _context.Applications
                    .Where(a => a.CreatedAt >= startDate)
                    .GroupBy(a => a.CreatedAt.Date)
                    .Select(g => new { date = g.Key, count = g.Count() })
                    .OrderBy(x => x.date)
                    .ToListAsync();

                var result = new List<object>();
                var currentDate = startDate;
                var cumulativeCount = await _context.Applications.Where(a => a.CreatedAt < startDate).CountAsync();

                while (currentDate <= endDate)
                {
                    var dayData = appGrowth.FirstOrDefault(x => x.date == currentDate);
                    var dailyCount = dayData?.count ?? 0;
                    cumulativeCount += dailyCount;

                    result.Add(new
                    {
                        date = currentDate.ToString("yyyy-MM-dd"),
                        dailyCount,
                        totalCount = cumulativeCount
                    });

                    currentDate = currentDate.AddDays(1);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении графика роста приложений");
                return BadRequest(new { error = "Ошибка получения данных графика" });
            }
        }

        [HttpGet("charts/downloads")]
        public async Task<IActionResult> GetDownloadsChart(int days = 30)
        {
            try
            {
                // Поскольку у нас нет истории скачиваний, создадим примерные данные
                // В реальном приложении здесь должна быть таблица с историей скачиваний
                var startDate = DateTime.UtcNow.AddDays(-days).Date;
                var endDate = DateTime.UtcNow.Date;

                var totalDownloads = await _context.Applications.SumAsync(a => a.DownloadCount);
                var appsCount = await _context.Applications.CountAsync();
                var avgDownloadsPerDay = appsCount > 0 ? totalDownloads / (days * appsCount) : 0;

                var result = new List<object>();
                var currentDate = startDate;
                var random = new Random();

                while (currentDate <= endDate)
                {
                    // Генерируем примерные данные на основе общей статистики
                    var dailyDownloads = random.Next(0, (int)(avgDownloadsPerDay * 2));
                    
                    result.Add(new
                    {
                        date = currentDate.ToString("yyyy-MM-dd"),
                        downloads = dailyDownloads
                    });

                    currentDate = currentDate.AddDays(1);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении графика скачиваний");
                return BadRequest(new { error = "Ошибка получения данных графика" });
            }
        }

        [HttpGet("charts/categories")]
        public async Task<IActionResult> GetCategoriesChart()
        {
            try
            {
                var categories = await _context.Applications
                    .Where(a => !string.IsNullOrEmpty(a.Category))
                    .GroupBy(a => a.Category)
                    .Select(g => new {
                        category = g.Key,
                        count = g.Count(),
                        downloads = g.Sum(a => a.DownloadCount)
                    })
                    .OrderByDescending(x => x.count)
                    .ToListAsync();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении графика категорий");
                return BadRequest(new { error = "Ошибка получения данных графика" });
            }
        }

        [HttpGet("system-info")]
        public async Task<IActionResult> GetSystemInfo()
        {
            try
            {
                // Информация о базе данных
                var dbSize = await GetDatabaseSize();
                var tablesInfo = await GetTablesInfo();

                // Информация о файлах
                var totalImages = await _context.Images.CountAsync();
                var totalImageSize = await _context.Images.SumAsync(i => (long?)i.Size) ?? 0;

                var result = new
                {
                    database = new
                    {
                        size = dbSize,
                        tables = tablesInfo
                    },
                    files = new
                    {
                        totalImages,
                        totalImageSize = FormatBytes(totalImageSize)
                    },
                    server = new
                    {
                        environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                        dotnetVersion = Environment.Version.ToString(),
                        osVersion = Environment.OSVersion.ToString(),
                        machineName = Environment.MachineName,
                        processorCount = Environment.ProcessorCount,
                        workingSet = FormatBytes(Environment.WorkingSet),
                        uptime = DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении системной информации");
                return BadRequest(new { error = "Ошибка получения системной информации" });
            }
        }

        private async Task<string> GetDatabaseSize()
        {
            try
            {
                // Для SQLite
                var connection = _context.Database.GetDbConnection();
                if (connection.GetType().Name.Contains("Sqlite"))
                {
                    var dbPath = connection.DataSource;
                    if (System.IO.File.Exists(dbPath))
                    {
                        var fileInfo = new FileInfo(dbPath);
                        return FormatBytes(fileInfo.Length);
                    }
                }
                return "Неизвестно";
            }
            catch
            {
                return "Неизвестно";
            }
        }

        private async Task<List<object>> GetTablesInfo()
        {
            try
            {
                var tables = new List<object>
                {
                    new { name = "Users", count = await _context.Users.CountAsync() },
                    new { name = "Applications", count = await _context.Applications.CountAsync() },
                    new { name = "Comments", count = await _context.Comments.CountAsync() },
                    new { name = "Ratings", count = await _context.Ratings.CountAsync() },
                    new { name = "Images", count = await _context.Images.CountAsync() }
                };

                return tables;
            }
            catch
            {
                return new List<object>();
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}