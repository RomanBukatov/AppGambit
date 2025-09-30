using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AppGambit.Data;
using AppGambit.Models;
using AppGambit.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

// Загружаем переменные окружения из .env файла только для Development
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    try
    {
        DotNetEnv.Env.Load();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
    }
}

Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// Добавление сервисов в контейнер
builder.Services.AddControllersWithViews(options =>
{
    // Включаем кэширование для контроллеров
    options.CacheProfiles.Add("Default", new Microsoft.AspNetCore.Mvc.CacheProfile
    {
        Duration = 300, // 5 минут
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any
    });
    options.CacheProfiles.Add("StaticContent", new Microsoft.AspNetCore.Mvc.CacheProfile
    {
        Duration = 86400, // 24 часа
        Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.Any
    });
});

// Добавляем сжатие ответов только для Production
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
            new[] { "application/javascript", "text/css", "text/json", "text/xml" });
    });
}

// Добавляем кэширование в памяти с оптимизированными настройками
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1024; // Ограничиваем размер кэша
    options.CompactionPercentage = 0.25; // Удаляем 25% записей при превышении лимита
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // Проверяем истекшие записи каждые 5 минут
});

builder.Services.AddResponseCaching(options =>
{
    options.MaximumBodySize = 64 * 1024; // 64KB максимальный размер тела ответа для кэширования
    options.UseCaseSensitivePaths = false;
});

// Настройка Entity Framework и Identity
// Строим строку подключения напрямую из переменных окружения для полной надежности
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? throw new InvalidOperationException("DB_USER environment variable not found.");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable not found.");
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? throw new InvalidOperationException("DB_NAME environment variable not found.");

var connectionString = $"Host={dbHost};Port={dbPort};Username={dbUser};Password={dbPassword};Database={dbName};Pooling=true;Minimum Pool Size=0;Maximum Pool Size=10;Connection Idle Lifetime=300;Connection Pruning Interval=10;Include Error Detail=true;Timeout=15;Command Timeout=30";

Console.WriteLine($"Built connection string: {connectionString}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(15);
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 2,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
    
    // Оптимизации для производительности
    options.EnableSensitiveDataLogging(false);
    options.EnableServiceProviderCaching();
    options.EnableDetailedErrors(false);
    
    // Отключаем отслеживание изменений по умолчанию для лучшей производительности
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

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

// Настройка куки аутентификации (сессии сохраняются 14 дней)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
});

// Настройка аутентификации через Google (только если настроены ключи)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

// Fallback к переменным окружения если конфигурация не сработала
if (string.IsNullOrEmpty(googleClientId))
    googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
if (string.IsNullOrEmpty(googleClientSecret))
    googleClientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");

if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            // Используем стандартный путь для Google OAuth
            options.CallbackPath = "/signin-google";
            
            // Добавляем дополнительные настройки для локальной разработки
            options.Events.OnRemoteFailure = context =>
            {
                var errorMessage = context.Failure?.Message ?? "Неизвестная ошибка";
                context.Response.Redirect("/Account/Login?error=" + Uri.EscapeDataString($"Ошибка Google OAuth: {errorMessage}. Проверьте настройки redirect URI в Google Console."));
                context.HandleResponse();
                return Task.CompletedTask;
            };
            
            // Настройки для работы с localhost
            options.SaveTokens = true;
        });
}

// Регистрация пользовательских сервисов
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IDatabaseImageService, DatabaseImageService>();
builder.Services.AddScoped<IDatabaseFileService, DatabaseFileService>();
builder.Services.AddScoped<ImageMigrationService>();
builder.Services.AddScoped<IUserCacheService, UserCacheService>();

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

// Включаем сжатие ответов только для Production
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}

// app.UseHttpsRedirection(); // Отключено для локальной разработки

// Настройка статических файлов с кэшированием
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Кэшируем статические файлы на 1 год
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
        ctx.Context.Response.Headers.Append("Expires", DateTime.UtcNow.AddYears(1).ToString("R"));
    }
});

// Включаем кэширование ответов
app.UseResponseCaching();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "application",
    pattern: "Applications/{id:int}",
    defaults: new { controller = "Applications", action = "Details" });

// Маршруты для стандартных действий должны быть перед маршрутом с именем
app.MapControllerRoute(
    name: "applicationActions",
    pattern: "Applications/{action}",
    defaults: new { controller = "Applications" },
    constraints: new { action = @"^(Create|Index|Search|AddComment|UpdateComment|DeleteComment|Rate|Download|Edit)$" });

app.MapControllerRoute(
    name: "applicationByName",
    pattern: "Applications/{name}",
    defaults: new { controller = "Applications", action = "DetailsByName" });

// Короткий маршрут для поиска пользователей
app.MapControllerRoute(
    name: "search",
    pattern: "Search",
    defaults: new { controller = "Profile", action = "Search" });

// Короткий маршрут для профилей пользователей - должен быть последним перед default
app.MapControllerRoute(
    name: "userProfileShort",
    pattern: "{displayName}",
    defaults: new { controller = "Profile", action = "ByDisplayName" },
    constraints: new { displayName = @"^(?!Home|Applications|Profile|Account|Cache|Admin|Search|api|css|js|lib|favicon\.ico|sw\.js).*$" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Инициализация ролей уже выполнена

app.Run();
