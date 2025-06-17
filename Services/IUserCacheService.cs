using AppGambit.Models;

namespace AppGambit.Services
{
    public interface IUserCacheService
    {
        Task<User?> GetCurrentUserAsync(string userId);
        void ClearUserCache(string userId);
    }
}