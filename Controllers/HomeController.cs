using System.Diagnostics;
using AppGambit.Models;
using AppGambit.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppGambit.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Получаем реальные данные из БД
                var totalApps = await _context.Applications.CountAsync();
                var totalUsers = await _context.Users.CountAsync();

                // Популярные приложения (по количеству скачиваний)
                var popularApps = await _context.Applications
                    .Include(a => a.User)
                    .Include(a => a.Ratings)
                    .OrderByDescending(a => a.DownloadCount)
                    .Take(8)
                    .ToListAsync();

                // Новые приложения
                var newApps = await _context.Applications
                    .Include(a => a.User)
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(8)
                    .ToListAsync();

                // Категории с количеством приложений
                var categories = await _context.Applications
                    .Where(a => !string.IsNullOrEmpty(a.Category))
                    .GroupBy(a => a.Category)
                    .Select(g => new { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(c => c.Count)
                    .Take(12)
                    .ToListAsync();

                ViewBag.PopularApps = popularApps;
                ViewBag.NewApps = newApps;
                ViewBag.Categories = categories;
                ViewBag.TotalApps = totalApps;
                ViewBag.TotalUsers = totalUsers;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке данных главной страницы");
                
                // Fallback данные в случае ошибки
                ViewBag.PopularApps = new List<Models.Application>();
                ViewBag.NewApps = new List<Models.Application>();
                ViewBag.Categories = new List<dynamic>();
                ViewBag.TotalApps = 0;
                ViewBag.TotalUsers = 0;

                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
