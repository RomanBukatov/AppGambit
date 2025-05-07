using AppGambit.Data;
using AppGambit.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Net.Http.Headers;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

var builder = WebApplication.CreateBuilder(args);

// Получаем строку подключения из конфигурации
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Для отладки выводим информацию о строке подключения
Console.WriteLine($"Using connection string: {connectionString?.Replace("Password=", "Password=*****")}");

// Проверяем, что строка подключения настроена
if (string.IsNullOrEmpty(connectionString) || 
    connectionString.Contains("Moved to user secrets or environment variables for security"))
{
    throw new InvalidOperationException(
        "Database connection string is not configured. " +
        "Please configure the connection string using user secrets, environment variables, or appsettings.Development.json. " +
        "See README.md for instructions.");
}

// Явно проверяем, что используется локальная база данных в режиме разработки
if (builder.Environment.IsDevelopment() && 
    !connectionString.Contains("localhost") && 
    !connectionString.Contains("127.0.0.1") && 
    !connectionString.Contains("supabase.co"))
{
    Console.WriteLine("WARNING: Development environment detected but non-local database connection found.");
    Console.WriteLine("Attempting to override with local development connection...");
    
    // Пытаемся использовать локальную базу данных
    connectionString = "Host=db.hfznocmfaojkemurarkj.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=fpfLfyys[LkzVtyz";
    Console.WriteLine($"Overridden connection string: {connectionString.Replace("Password=", "Password=*****")}");
}

// Add database context with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    
    // В режиме разработки включаем детальное логирование SQL-запросов
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => 
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;         // Отключаем требование цифр
    options.Password.RequireLowercase = false;     // Отключаем требование строчных букв
    options.Password.RequireUppercase = false;     // Отключаем требование заглавных букв
    options.Password.RequireNonAlphanumeric = false; // Отключаем требование спецсимволов
    options.Password.RequiredLength = 6;           // Уменьшаем минимальную длину до 6 символов
    options.SignIn.RequireConfirmedEmail = false;  // Отключаем подтверждение email
    options.User.RequireUniqueEmail = true;        // Email должен быть уникальным
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Настройка аутентификации через cookie
builder.Services.ConfigureApplicationCookie(options => 
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax; // SameSite=Lax для совместимости с Google OAuth
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.IsEssential = true;
    
    // Обработчик событий для отладки и устранения проблем с аутентификацией
    options.Events.OnRedirectToLogin = context => 
    {
        Console.WriteLine($"Redirecting to login: {context.RedirectUri}");
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    
    // Устанавливаем куки сразу после входа
    options.Events.OnSigningIn = context =>
    {
        Console.WriteLine($"Signing in user: {context.Principal?.Identity?.Name}");
        
        // Добавляем дополнительную куку для немедленного определения состояния авторизации
        context.Response.Cookies.Append("UserAuthenticated", "true", new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(options.ExpireTimeSpan),
            IsEssential = true
        });
            
        return Task.CompletedTask;
    };
});

// Добавляем настройки проверки состояния для внешней аутентификации
builder.Services.AddAuthentication(options => 
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

// Настройка внешних провайдеров аутентификации
builder.Services.AddAuthentication()
    // Добавляем аутентификацию через Google
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        
        // Базовая конфигурация
        googleOptions.CallbackPath = "/signin-google";
        googleOptions.SaveTokens = true;
        
        // В режиме разработки отключаем проверку состояния, если это настроено
        if (builder.Environment.IsDevelopment() && 
            builder.Configuration.GetValue<bool>("Authentication:SkipStateValidationInDevelopment"))
        {
            googleOptions.CorrelationCookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
            googleOptions.Events = new OAuthEvents
            {
                OnRemoteFailure = context =>
                {
                    context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                        .LogError($"Ошибка Google авторизации: {context.Failure?.Message}");
                    
                    context.Response.Redirect("/Account/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
                    context.HandleResponse();
                    
                    return Task.CompletedTask;
                }
            };
        }
        else
        {
            // Стандартный обработчик ошибок
            googleOptions.Events = new OAuthEvents
            {
                OnRemoteFailure = context =>
                {
                    // Логирование ошибки
                    context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>()
                        .LogError($"Ошибка Google авторизации: {context.Failure?.Message}");
                    
                    // Перенаправление на страницу входа с ошибкой
                    context.Response.Redirect("/Account/Login?error=" + Uri.EscapeDataString(context.Failure?.Message ?? "Unknown error"));
                    context.HandleResponse();
                    
                    return Task.CompletedTask;
                }
            };
        }
    });

// Add Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/javascript", "text/css", "application/json", "image/svg+xml" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Add Response Caching
builder.Services.AddResponseCaching();

// Add Output Caching
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(policy => policy.Cache());
    options.AddPolicy("HomePage", policy => policy.Cache().Expire(TimeSpan.FromMinutes(10)));
});

// Add services to the container
builder.Services.AddControllersWithViews();

// Настраиваем антиподделку для форм
builder.Services.AddAntiforgery(options => 
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
    options.Cookie.HttpOnly = true;
});

// Настраиваем правильное хранение состояния для корреляции OAuth
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppGambit", "DataProtection-Keys")));

// Add session to improve OAuth correlation
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.Name = ".AppGambit.Session"; // Устанавливаем явное имя для куки сессии
});

// Настраиваем параметры cookies для всего приложения
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = Microsoft.AspNetCore.Http.SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always; 
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.CheckConsentNeeded = context => false; // Отключаем запрос на согласие с cookie
    // Разрешаем кросс-доменные cookie
    options.OnAppendCookie = cookieContext =>
    {
        if (cookieContext.CookieName.StartsWith(".AspNetCore."))
        {
            cookieContext.CookieOptions.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
            cookieContext.CookieOptions.Secure = true;
        }
    };
});

// Add distributed memory cache for OAuth state
builder.Services.AddDistributedMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // В режиме разработки показываем подробную информацию об ошибках
    app.UseDeveloperExceptionPage();
}

// Настройка для обработки ошибок аутентификации
// Отключаем стандартные обработчики ошибок для Google OAuth
app.Use(async (context, next) => 
{
    await next();
    
    // Если произошла ошибка аутентификации, выводим её детали
    if (context.Response.StatusCode == 401 || context.Response.StatusCode == 403)
    {
        Console.WriteLine($"Ошибка аутентификации: Код {context.Response.StatusCode}");
        Console.WriteLine($"Путь: {context.Request.Path}");
        Console.WriteLine($"Запрос: {context.Request.QueryString}");
        
        // Выводим все заголовки для отладки
        foreach (var header in context.Request.Headers)
        {
            Console.WriteLine($"Заголовок: {header.Key}={header.Value}");
        }
        
        // Выводим все cookies
        foreach (var cookie in context.Request.Cookies)
        {
            Console.WriteLine($"Cookie: {cookie.Key}={cookie.Value}");
        }
    }
});

// Enable compression
app.UseResponseCompression();

// Enable caching
app.UseResponseCaching();
app.UseOutputCache();

// Cache static files
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        const int durationInSeconds = 60 * 60 * 24 * 7; // 7 дней
        ctx.Context.Response.Headers[HeaderNames.CacheControl] = 
            "public,max-age=" + durationInSeconds;
    }
});

app.UseHttpsRedirection();

// Включаем cookie policy
app.UseCookiePolicy();

// Включаем сессии ДО аутентификации и авторизации
app.UseSession();

app.UseRouting();

// Включаем аутентификацию и авторизацию
app.UseAuthentication();
app.UseAuthorization();

// Настраиваем правила маршрутизации
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
