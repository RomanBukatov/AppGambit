using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;

namespace AppGambit.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ThemeController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<ThemeController> _logger;

        public ThemeController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<ThemeController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet("current")]
        public async Task<IActionResult> GetCurrentTheme()
        {
            try
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userId = _userManager.GetUserId(User);
                    var user = await _context.Users.FindAsync(userId);
                    
                    if (user != null && !string.IsNullOrEmpty(user.PreferredTheme))
                    {
                        return Ok(new { theme = user.PreferredTheme, source = "user" });
                    }
                }

                // Возвращаем системную тему по умолчанию
                return Ok(new { theme = "light", source = "default" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении текущей темы");
                return Ok(new { theme = "light", source = "default" });
            }
        }

        [HttpPost("set")]
        [Authorize]
        public async Task<IActionResult> SetTheme([FromBody] SetThemeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Theme) || 
                    (request.Theme != "light" && request.Theme != "dark" && request.Theme != "auto"))
                {
                    return BadRequest(new { error = "Недопустимое значение темы" });
                }

                var userId = _userManager.GetUserId(User);
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null)
                {
                    return NotFound(new { error = "Пользователь не найден" });
                }

                user.PreferredTheme = request.Theme;
                user.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Пользователь {UserId} изменил тему на {Theme}", userId, request.Theme);

                return Ok(new { 
                    message = "Тема успешно сохранена", 
                    theme = request.Theme 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при сохранении темы для пользователя {UserId}", _userManager.GetUserId(User));
                return BadRequest(new { error = "Ошибка сохранения темы" });
            }
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetThemeStats()
        {
            try
            {
                var stats = await _context.Users
                    .Where(u => !string.IsNullOrEmpty(u.PreferredTheme))
                    .GroupBy(u => u.PreferredTheme)
                    .Select(g => new { theme = g.Key, count = g.Count() })
                    .ToListAsync();

                var totalUsers = await _context.Users.CountAsync();
                var usersWithTheme = stats.Sum(s => s.count);
                var usersWithoutTheme = totalUsers - usersWithTheme;

                var result = new
                {
                    totalUsers,
                    usersWithTheme,
                    usersWithoutTheme,
                    themeDistribution = stats,
                    defaultThemeUsers = usersWithoutTheme
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики тем");
                return BadRequest(new { error = "Ошибка получения статистики" });
            }
        }
    }

    public class SetThemeRequest
    {
        public string Theme { get; set; } = string.Empty;
    }
}