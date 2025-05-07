using System.Diagnostics;
using System.Linq;
using AppGambit.Data;
using AppGambit.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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

        public async Task<IActionResult> Index(string searchTerm = null, string tag = null, int page = 1)
        {
            // Добавляем заголовки, предотвращающие кэширование при наличии параметра auth_refresh
            if (Request.Query.ContainsKey("auth_refresh"))
            {
                Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                Response.Headers.Append("Pragma", "no-cache");
                Response.Headers.Append("Expires", "0");
            }

            const int pageSize = 12;
            
            // Базовый запрос
            var query = _context.Programs
                .Include(p => p.Author)
                .Include(p => p.ProgramTags)
                    .ThenInclude(pt => pt.Tag)
                .AsQueryable();
                
            // Применение фильтров
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(p => p.Title.ToLower().Contains(searchTerm) || 
                                         p.Description.ToLower().Contains(searchTerm));
            }
            
            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(p => p.ProgramTags.Any(pt => pt.Tag.Name.ToLower() == tag.ToLower()));
            }
            
            // Получение общего количества результатов
            var totalItems = await query.CountAsync();
            
            // Пагинация
            var programs = await query
                .OrderByDescending(p => p.UploadDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            
            // Получение популярных тегов
            var popularTags = await _context.Tags
                .OrderByDescending(t => t.ProgramTags.Count)
                .Take(20)
                .ToListAsync();
            
            // Подготовка модели представления
            var viewModel = new HomeViewModel
            {
                Programs = programs,
                PopularTags = popularTags,
                SearchTerm = searchTerm,
                SelectedTag = tag,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize),
                TotalItems = totalItems
            };
            
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string message = null)
        {
            var errorVM = new ErrorViewModel 
            { 
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ErrorMessage = message,
                Query = HttpContext.Request.QueryString.ToString(),
                Path = HttpContext.Request.Path,
                Cookies = string.Join(", ", HttpContext.Request.Cookies.Select(c => c.Key))
            };
            
            // Логируем детали ошибки
            _logger.LogError($"Ошибка: {message} | RequestId: {errorVM.RequestId} | Path: {errorVM.Path}");
            
            return View(errorVM);
        }
    }
}
