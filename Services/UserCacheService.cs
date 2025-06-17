using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using AppGambit.Models;

namespace AppGambit.Services
{
    public class UserCacheService : IUserCacheService
    {
        private readonly UserManager<User> _userManager;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UserCacheService> _logger;

        public UserCacheService(
            UserManager<User> userManager,
            IMemoryCache cache,
            ILogger<UserCacheService> logger)
        {
            _userManager = userManager;
            _cache = cache;
            _logger = logger;
        }

        public async Task<User?> GetCurrentUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            var cacheKey = $"user_{userId}";

            if (!_cache.TryGetValue(cacheKey, out User? user))
            {
                user = await _userManager.FindByIdAsync(userId);
                
                if (user != null)
                {
                    // Кэшируем пользователя на 5 минут
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                        SlidingExpiration = TimeSpan.FromMinutes(2),
                        Priority = CacheItemPriority.Normal,
                        Size = 1 // Указываем размер записи для кэша
                    };

                    _cache.Set(cacheKey, user, cacheOptions);
                    _logger.LogDebug($"Пользователь {userId} загружен в кэш");
                }
            }

            return user;
        }

        public void ClearUserCache(string userId)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                var cacheKey = $"user_{userId}";
                _cache.Remove(cacheKey);
                _logger.LogDebug($"Кэш пользователя {userId} очищен");
            }
        }
    }
}