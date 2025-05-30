using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;
using AppGambit.Services;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер
builder.Services.AddControllersWithViews();

// Настройка Entity Framework и Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Настройка аутентификации через Google
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        // Используем стандартный путь для Google OAuth
        options.CallbackPath = "/signin-google";
        
        // Добавляем дополнительные настройки для локальной разработки
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/Login?error=" + context.Failure?.Message);
            context.HandleResponse();
            return Task.CompletedTask;
        };
        
        // Настройки для работы с localhost
        options.SaveTokens = true;
    });

// Регистрация пользовательских сервисов
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IImageService, ImageService>();

// Настройка загрузки файлов
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

var app = builder.Build();

// Настройка конвейера HTTP-запросов
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "profileByDisplayName",
    pattern: "u/{displayName}",
    defaults: new { controller = "Profile", action = "ByDisplayName" });

app.MapControllerRoute(
    name: "application",
    pattern: "app/{id:int}",
    defaults: new { controller = "Applications", action = "Details" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
