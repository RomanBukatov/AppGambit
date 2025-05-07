using AppGambit.Data;
using AppGambit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data.Common;
using System.Text.Json;
using Npgsql;

namespace AppGambit.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            ApplicationDbContext dbContext,
            IConfiguration configuration,
            ILogger<DiagnosticsController> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("connection")]
        public IActionResult GetConnectionInfo()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                
                // Скрываем пароль для безопасности
                var sanitizedConnectionString = SanitizeConnectionString(connectionString);
                
                // Проверяем соединение
                _dbContext.Database.OpenConnection();
                var connectionState = _dbContext.Database.GetDbConnection().State.ToString();
                _dbContext.Database.CloseConnection();
                
                var connectionInfo = new 
                {
                    DatabaseProvider = _dbContext.Database.ProviderName,
                    ConnectionString = sanitizedConnectionString,
                    ConnectionState = connectionState,
                    ServerVersion = _dbContext.Database.GetDbConnection().ServerVersion
                };
                
                return Ok(connectionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о соединении с базой данных");
                return StatusCode(500, new { error = "Ошибка при получении информации о соединении", details = ex.Message });
            }
        }
        
        [HttpGet("userprofiles")]
        public async Task<IActionResult> GetUserProfiles()
        {
            try
            {
                // Получаем информацию о первых 10 профилях пользователей
                var profiles = await _dbContext.UserProfiles
                    .AsNoTracking()
                    .Take(10)
                    .Select(p => new 
                    { 
                        p.UserId, 
                        p.DisplayName, 
                        p.AvatarUrl,
                        p.Location 
                    })
                    .ToListAsync();
                
                // Проверяем настройки таблицы
                var tableInfo = await GetTableInfo("user_profiles");
                
                return Ok(new 
                { 
                    Profiles = profiles,
                    TableInfo = tableInfo,
                    Count = await _dbContext.UserProfiles.CountAsync()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении профилей пользователей");
                return StatusCode(500, new { error = "Ошибка при получении профилей", details = ex.Message });
            }
        }
        
        [HttpPost("updateprofile")]
        public async Task<IActionResult> UpdateUserProfile([FromBody] UpdateProfileRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                return BadRequest("UserId не может быть пустым");
            }
            
            try
            {
                // Находим профиль пользователя
                var profile = await _dbContext.UserProfiles
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.UserId == request.UserId);
                
                if (profile == null)
                {
                    return NotFound($"Профиль с UserId={request.UserId} не найден");
                }
                
                // Сохраняем предыдущее значение для отчета
                string previousDisplayName = profile.DisplayName;
                
                // Обновляем DisplayName
                profile.DisplayName = request.DisplayName;
                
                // Сохраняем изменения
                await _dbContext.SaveChangesAsync();
                
                // Перезагружаем профиль для проверки
                await _dbContext.Entry(profile).ReloadAsync();
                
                return Ok(new 
                { 
                    Success = true,
                    Message = $"Профиль успешно обновлен",
                    PreviousDisplayName = previousDisplayName,
                    CurrentDisplayName = profile.DisplayName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении профиля пользователя");
                return StatusCode(500, new { error = "Ошибка при обновлении профиля", details = ex.Message });
            }
        }
        
        [HttpPost("updatedisplayname")]
        public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateUserNameRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.NewDisplayName))
            {
                return BadRequest("UserId и NewDisplayName не могут быть пустыми");
            }
            
            try
            {
                // Сначала пробуем обновить через Entity Framework
                var profile = await _dbContext.UserProfiles
                    .FirstOrDefaultAsync(p => p.UserId == request.UserId);
                
                if (profile == null)
                {
                    return NotFound($"Профиль с UserId={request.UserId} не найден");
                }
                
                // Сохраняем предыдущее значение для отчета
                string previousDisplayName = profile.DisplayName;
                
                // Обновляем DisplayName
                profile.DisplayName = request.NewDisplayName;
                
                // Принудительно отмечаем сущность как измененную
                _dbContext.Entry(profile).State = EntityState.Modified;
                
                // Сохраняем изменения
                await _dbContext.SaveChangesAsync();
                
                // Перезагружаем профиль для проверки
                await _dbContext.Entry(profile).ReloadAsync();
                
                // Если имя не обновилось через EF, пробуем SQL
                bool usedSql = false;
                if (profile.DisplayName != request.NewDisplayName)
                {
                    // Формируем SQL-запрос (с параметризацией для избежания SQL-инъекций)
                    var sql = "UPDATE user_profiles SET display_name = @p1 WHERE user_id = @p2";
                    
                    // Выполняем запрос
                    await _dbContext.Database.ExecuteSqlRawAsync(sql, 
                        new NpgsqlParameter("p1", request.NewDisplayName),
                        new NpgsqlParameter("p2", request.UserId));
                    
                    // Перезагружаем для проверки
                    await _dbContext.Entry(profile).ReloadAsync();
                    usedSql = true;
                }
                
                // Проверяем текущее состояние всех профилей
                var allProfiles = await _dbContext.UserProfiles
                    .Where(p => p.DisplayName == request.NewDisplayName || p.UserId == request.UserId)
                    .ToListAsync();
                
                return Ok(new 
                { 
                    Success = true,
                    Message = usedSql ? "Профиль обновлен через SQL" : "Профиль обновлен через EF",
                    PreviousDisplayName = previousDisplayName,
                    CurrentDisplayName = profile.DisplayName,
                    MatchingProfiles = allProfiles.Select(p => new { p.UserId, p.DisplayName })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении имени пользователя");
                return StatusCode(500, new { error = "Ошибка при обновлении имени", details = ex.Message });
            }
        }
        
        [HttpGet("testprofile/{userId}")]
        public async Task<IActionResult> TestProfile(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest("UserId не может быть пустым");
            }
            
            try
            {
                // 1. Проверяем данные в user_profiles с помощью прямого SQL
                var profileData = await _dbContext.Database.SqlQueryRaw<dynamic>(@"
                    SELECT * FROM user_profiles WHERE user_id = @userId",
                    new NpgsqlParameter("userId", userId))
                    .ToListAsync();
                
                // 2. Получаем данные из Identity
                var user = await _dbContext.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user == null)
                {
                    return NotFound($"Пользователь с ID {userId} не найден");
                }
                
                // 3. Тестируем обновление имени пользователя
                var testNewName = $"TestUser_{DateTime.Now.Ticks % 1000}";
                
                return Ok(new 
                {
                    UserId = userId,
                    SqlQueryResults = profileData,
                    IdentityUser = new 
                    {
                        Id = user.Id,
                        Email = user.Email,
                        UserName = user.UserName,
                        ProfileData = user.Profile != null ? new 
                        {
                            DisplayName = user.Profile.DisplayName,
                            Bio = user.Profile.Bio,
                            AvatarUrl = user.Profile.AvatarUrl
                        } : null
                    },
                    TestData = new 
                    {
                        SuggestedNewName = testNewName,
                        UpdateUrl = $"/api/Diagnostics/updatedisplayname",
                        RequestBody = new { UserId = userId, NewDisplayName = testNewName },
                        Instructions = "Для проверки, отправьте POST запрос на указанный URL с указанным телом запроса"
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при тестировании профиля");
                return StatusCode(500, new { error = "Ошибка при тестировании профиля", details = ex.Message });
            }
        }
        
        private async Task<object> GetTableInfo(string tableName)
        {
            try
            {
                // SQL-запрос для получения информации о колонках таблицы
                string sql = @"
                    SELECT 
                        column_name, 
                        data_type,
                        is_nullable,
                        column_default
                    FROM 
                        information_schema.columns 
                    WHERE 
                        table_name = @tableName";
                
                var columns = await _dbContext.Database.SqlQueryRaw<ColumnInfo>(sql, new DbParameter[] 
                {
                    new Npgsql.NpgsqlParameter("tableName", tableName)
                }).ToListAsync();
                
                // SQL-запрос для получения информации об индексах
                string indexSql = @"
                    SELECT
                        i.relname as index_name,
                        a.attname as column_name,
                        idx.indisunique as is_unique
                    FROM
                        pg_class t,
                        pg_class i,
                        pg_index idx,
                        pg_attribute a
                    WHERE
                        t.oid = idx.indrelid
                        AND i.oid = idx.indexrelid
                        AND a.attrelid = t.oid
                        AND a.attnum = ANY(idx.indkey)
                        AND t.relkind = 'r'
                        AND t.relname = @tableName
                    ORDER BY
                        i.relname;";
                
                var indexes = await _dbContext.Database.SqlQueryRaw<IndexInfo>(indexSql, new DbParameter[] 
                {
                    new Npgsql.NpgsqlParameter("tableName", tableName)
                }).ToListAsync();
                
                return new 
                { 
                    TableName = tableName,
                    Columns = columns,
                    Indexes = indexes
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении информации о таблице {tableName}");
                return new { Error = ex.Message };
            }
        }
        
        private string SanitizeConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Не указан";
            
            // Скрываем пароль в строке подключения
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"Password=([^;]*)", 
                "Password=********"
            );
        }
        
        public class ColumnInfo
        {
            public string column_name { get; set; }
            public string data_type { get; set; }
            public string is_nullable { get; set; }
            public string column_default { get; set; }
        }
        
        public class IndexInfo
        {
            public string index_name { get; set; }
            public string column_name { get; set; }
            public bool is_unique { get; set; }
        }
        
        public class UpdateProfileRequest
        {
            public string UserId { get; set; }
            public string DisplayName { get; set; }
        }
        
        public class UpdateUserNameRequest
        {
            public string UserId { get; set; }
            public string NewDisplayName { get; set; }
        }
    }
} 