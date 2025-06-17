using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using AppGambit.Models;

namespace AppGambit.Services
{
    public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<User, IdentityRole>
    {
        public CustomUserClaimsPrincipalFactory(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor)
            : base(userManager, roleManager, optionsAccessor)
        {
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // Добавляем дополнительные Claims для оптимизации
            if (!string.IsNullOrEmpty(user.DisplayName))
            {
                identity.AddClaim(new Claim("DisplayName", user.DisplayName));
            }

            if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            {
                identity.AddClaim(new Claim("ProfileImageUrl", user.ProfileImageUrl));
            }

            // Добавляем Email как отдельный Claim для быстрого доступа
            if (!string.IsNullOrEmpty(user.Email))
            {
                identity.AddClaim(new Claim("UserEmail", user.Email));
            }

            return identity;
        }
    }
}