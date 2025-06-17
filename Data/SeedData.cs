using Microsoft.AspNetCore.Identity;
using AppGambit.Models;

namespace AppGambit.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            // Создаем роль Admin, если она не существует
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // Создаем роль User, если она не существует
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Назначаем роль администратора существующему пользователю
            var adminEmail = "admin@appgambit.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var result = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (result.Succeeded)
                {
                    Console.WriteLine($"Пользователь {adminEmail} назначен администратором.");
                }
            }
        }
    }
}