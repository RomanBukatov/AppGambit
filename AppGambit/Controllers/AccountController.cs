using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AppGambit.Models;
using AppGambit.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Npgsql;

namespace AppGambit.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _env;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _env = env;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Проверяем, существует ли уже пользователь с таким email
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    // Проверяем, имеет ли существующий пользователь внешние логины
                    var logins = await _userManager.GetLoginsAsync(existingUser);
                    if (logins.Any(l => l.LoginProvider == "Google"))
                    {
                        ModelState.AddModelError(string.Empty, "Этот email уже зарегистрирован через Google. Пожалуйста, используйте вход через Google.");
                        return View(model);
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Пользователь с таким email уже существует.");
                        return View(model);
                    }
                }

                // Проверка на пустое имя пользователя
                if (string.IsNullOrWhiteSpace(model.DisplayName))
                {
                    ModelState.AddModelError("DisplayName", "Имя пользователя не может быть пустым");
                    return View(model);
                }

                var user = new ApplicationUser 
                { 
                    UserName = model.Email, 
                    Email = model.Email,
                    RegistrationDate = DateTime.UtcNow,
                    EmailConfirmed = true // В реальном приложении здесь должна быть логика подтверждения email
                };
                
                var result = await _userManager.CreateAsync(user, model.Password);
                
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Пользователь создал новую учетную запись с паролем. Email: {model.Email}, DisplayName: {model.DisplayName}");
                    
                    // Создаем профиль пользователя
                    var profile = new UserProfile
                    {
                        UserId = user.Id,
                        DisplayName = model.DisplayName // Используем DisplayName из формы регистрации
                    };
                    
                    // Добавляем профиль в базу данных
                    var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
                    dbContext.UserProfiles.Add(profile);
                    
                    try 
                    {
                        await dbContext.SaveChangesAsync();
                        _logger.LogInformation($"Создан профиль пользователя с DisplayName: {profile.DisplayName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при сохранении профиля пользователя");
                        ModelState.AddModelError(string.Empty, "Произошла ошибка при создании профиля пользователя");
                        return View(model);
                    }
                    
                    // Выполняем вход пользователя в систему
                    await _signInManager.SignInAsync(user, isPersistent: true);
                    
                    // Добавляем куки для обновления состояния авторизации
                    Response.Cookies.Append("SessionRefresh", "true", new CookieOptions 
                    { 
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Устанавливаем куки для немедленного определения аутентификации на стороне клиента
                    Response.Cookies.Append("UserAuthenticated", "true", new CookieOptions
                    {
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Перенаправляем на профиль пользователя
                    return RedirectToAction("MyProfile");
                }
                
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Если пользователь уже вошел в систему, перенаправляем его
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl ?? Url.Content("~/"));
            }
            
            // Очистка существующего внешнего файла cookie для обеспечения чистого процесса входа
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            
            // Проверяем, принимает ли браузер куки вообще
            string testCookieName = "CookieTestForAuth";
            HttpContext.Response.Cookies.Append(testCookieName, "test", new CookieOptions 
            { 
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None,
                Secure = true,
                HttpOnly = true,
                Expires = DateTimeOffset.Now.AddMinutes(5)
            });

            ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");
            
            // Если есть auth_refresh, значит перенаправление после авторизации
            if (Request.Query.ContainsKey("auth_refresh"))
            {
                // Добавим задержку для уверенности, что куки успеют применится
                TempData["RedirectTo"] = Url.Content("~/");
                return RedirectToAction("RedirectAfterLogin");
            }
            
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("Начало процесса входа по логину/паролю");
            
            ViewData["ReturnUrl"] = returnUrl ?? Url.Content("~/");
            
            if (ModelState.IsValid)
            {
                // Проверяем, существует ли пользователь с таким email
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Проверяем, имеет ли пользователь внешние логины
                    var logins = await _userManager.GetLoginsAsync(user);
                    if (logins.Any(l => l.LoginProvider == "Google") && await _userManager.HasPasswordAsync(user) == false)
                    {
                        // Если пользователь зарегистрирован через Google и не имеет пароля,
                        // предлагаем войти через Google
                        ModelState.AddModelError(string.Empty, "Этот аккаунт был зарегистрирован через Google. Пожалуйста, используйте вход через Google.");
                        return View(model);
                    }
                }
                
                var signInStartTime = DateTime.UtcNow;
                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, 
                    model.Password, 
                    model.RememberMe, 
                    lockoutOnFailure: true);
                
                _logger.LogInformation($"Аутентификация заняла: {(DateTime.UtcNow - signInStartTime).TotalMilliseconds}ms");
                
                if (result.Succeeded)
                {
                    _logger.LogInformation("Пользователь вошел в систему");
                    
                    // Обновляем дату входа асинхронно, не блокируя основной поток
                    _ = Task.Run(async () => 
                    {
                        try 
                        {
                            var user = await _userManager.FindByEmailAsync(model.Email);
                            if (user != null)
                            {
                                user.LastLoginDate = DateTime.UtcNow;
                                await _userManager.UpdateAsync(user);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Ошибка при обновлении даты последнего входа");
                        }
                    });
                    
                    // Добавляем куки для обновления состояния авторизации
                    Response.Cookies.Append("SessionRefresh", "true", new CookieOptions 
                    { 
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Устанавливаем флаг аутентификации для клиентской стороны
                    Response.Cookies.Append("UserAuthenticated", "true", new CookieOptions
                    {
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Проверяем, является ли returnUrl локальным
                    if (Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        // По умолчанию перенаправляем на профиль пользователя
                        return RedirectToAction("MyProfile");
                    }
                }
                
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Учетная запись пользователя заблокирована");
                    return RedirectToAction(nameof(Lockout));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Неудачная попытка входа в систему");
                    return View(model);
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Lockout()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Очищаем все типы куки аутентификации
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            await _signInManager.SignOutAsync();

            // Очищаем все куки связанные с состоянием аутентификации
            foreach (var cookie in Request.Cookies.Keys)
            {
                // Удаляем все куки кроме антифоргери токена
                if (!cookie.StartsWith("__RequestVerificationToken"))
                {
                    Response.Cookies.Delete(cookie);
                }
            }

            // Явно удаляем ключевые куки идентификации
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            Response.Cookies.Delete("UserAuthenticated");
            Response.Cookies.Delete("SessionRefresh");
            Response.Cookies.Delete(".AspNetCore.Session");

            // Очищаем локальное хранилище через JavaScript
            TempData["ClearAuthState"] = "true";
            
            _logger.LogInformation("Пользователь вышел из системы");
            
            // Используем временный redirect через промежуточную страницу для обеспечения корректного выхода
            return RedirectToAction("LogoutConfirm", "Account");
        }

        [HttpGet]
        public IActionResult LogoutConfirm()
        {
            // Отправляем пользователя на главную страницу с принудительным обновлением состояния
            return View();
        }

        [HttpGet]
        public IActionResult RedirectAfterLogin()
        {
            // Получаем URL перенаправления из TempData
            string redirectTo = TempData["RedirectTo"] as string;
            
            // Если URL не определен, перенаправляем на главную
            if (string.IsNullOrEmpty(redirectTo))
            {
                redirectTo = Url.Action("Index", "Home");
            }
            
            // Отображаем страницу с JavaScript, который выполнит перенаправление после задержки
            ViewBag.RedirectTo = redirectTo;
            ViewBag.DisplayNameChanged = TempData["DisplayNameChanged"] as string;
            ViewBag.NewDisplayName = TempData["NewDisplayName"] as string;
            ViewBag.StatusMessage = TempData["StatusMessage"] as string;
            
            return View();
        }

        [HttpGet]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnUrl = null)
        {
            // Если пользователь уже вошел в систему, перенаправляем его
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToLocal(returnUrl ?? Url.Content("~/"));
            }
            
            // Упрощаем returnUrl, если он null
            returnUrl = returnUrl ?? Url.Content("~/");
            
            _logger.LogInformation($"Запрос на внешнюю авторизацию: {provider}, returnUrl: {returnUrl}, метод: {Request.Method}");
            
            try
            {
                // Формируем корректный URI для обратного вызова
                var redirectUri = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
                _logger.LogInformation($"RedirectUri: {redirectUri}");
                
                // Используем стандартный механизм вызова внешнего провайдера
            var properties = new AuthenticationProperties 
            { 
                    RedirectUri = redirectUri,
                    AllowRefresh = true,
                    IsPersistent = true,
                    Items = 
                    { 
                    { "returnUrl", returnUrl },
                        { "scheme", provider },
                        { "loginProvider", provider }
                }
            };
            
            return Challenge(properties, provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при вызове внешней авторизации");
                TempData["ErrorMessage"] = "Произошла ошибка при попытке авторизации через внешний сервис. Пожалуйста, попробуйте снова.";
                return RedirectToAction(nameof(Login));
            }
        }

        [HttpGet]
        [Route("/signin-google")]
        public async Task<IActionResult> GoogleCallback(string code, string state = null, string returnUrl = null, string error = null)
        {
            // Просто логируем вызов этого метода для отладки
            _logger.LogInformation($"Вызван GoogleCallback: code={code != null}, state={state}, error={error}");
            
            if (error != null)
            {
                _logger.LogError($"Ошибка Google OAuth: {error}");
                TempData["ErrorMessage"] = $"Ошибка при авторизации через Google: {error}";
                return RedirectToAction("Login");
            }
            
            // Редирект на стандартную обработку ASP.NET Core Identity
            // Этот метод не должен вызываться при корректной конфигурации
            return RedirectToAction("ExternalLoginCallback", new { returnUrl });
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            
            // Обработка явной ошибки
            if (remoteError != null)
            {
                _logger.LogError($"Явная ошибка от внешнего провайдера: {remoteError}");
                TempData["ErrorMessage"] = $"Ошибка при входе через внешний сервис: {remoteError}";
                return RedirectToAction(nameof(Login));
            }
            
            // Обработка ошибки из query string
            if (Request.Query.ContainsKey("error"))
            {
                var error = Request.Query["error"];
                _logger.LogError($"Ошибка из query string: {error}");
                TempData["ErrorMessage"] = $"Ошибка при входе через внешний сервис: {error}";
                return RedirectToAction(nameof(Login));
            }
            
            try
            {
                // Получаем информацию о входе от внешнего провайдера
                var info = await _signInManager.GetExternalLoginInfoAsync();
                
                if (info == null)
                {
                    _logger.LogError("Ошибка получения информации от внешнего провайдера");
                    TempData["ErrorMessage"] = "Не удалось получить информацию от внешнего провайдера. Пожалуйста, попробуйте снова.";
                    return RedirectToAction(nameof(Login));
                }
                
                // Логируем успешное получение информации
                _logger.LogInformation($"Успешно получена информация от провайдера {info.LoginProvider}");
                
                // Пытаемся войти с помощью внешнего провайдера
                var signInResult = await _signInManager.ExternalLoginSignInAsync(
                    info.LoginProvider, 
                    info.ProviderKey, 
                    isPersistent: true,
                    bypassTwoFactor: true);
                
                if (signInResult.Succeeded)
                {
                    // Пользователь найден и успешно вошел
                    _logger.LogInformation($"Пользователь вошел с помощью {info.LoginProvider}");
                                return RedirectToLocal(returnUrl);
                            }
                
                // Если пользователь не найден, создаем нового
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                
                if (string.IsNullOrEmpty(email))
                {
                    TempData["ErrorMessage"] = "Email не получен от внешнего сервиса";
                    return RedirectToAction(nameof(Login));
                }
                
                // Проверяем, существует ли уже пользователь с таким email
                var user = await _userManager.FindByEmailAsync(email);
                
                if (user == null)
                {
                    // Создаем нового пользователя
                    var name = info.Principal.FindFirstValue(ClaimTypes.Name);
                    
                    user = new ApplicationUser 
                    { 
                        UserName = email,
                        Email = email,
                        RegistrationDate = DateTime.UtcNow,
                        EmailConfirmed = true
                    };
                    
                    var createResult = await _userManager.CreateAsync(user);
                    
                    if (!createResult.Succeeded)
                    {
                        _logger.LogError($"Ошибка при создании пользователя: {string.Join(", ", createResult.Errors)}");
                        TempData["ErrorMessage"] = $"Ошибка при создании пользователя: {string.Join(", ", createResult.Errors)}";
                        return RedirectToAction(nameof(Login));
                    }
                    
                    // Создаем профиль пользователя
                    var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
                    
                    var profile = new UserProfile
                    {
                        UserId = user.Id,
                        DisplayName = name ?? email.Split('@')[0]
                    };
                    
                    dbContext.UserProfiles.Add(profile);
                    await dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation($"Создан новый пользователь {email}");
                }
                
                // Добавляем внешний логин к пользователю
                var addLoginResult = await _userManager.AddLoginAsync(user, info);
                
                if (!addLoginResult.Succeeded)
                {
                    _logger.LogError($"Ошибка при добавлении внешнего логина: {string.Join(", ", addLoginResult.Errors)}");
                    TempData["ErrorMessage"] = $"Ошибка при добавлении внешнего логина: {string.Join(", ", addLoginResult.Errors)}";
                    return RedirectToAction(nameof(Login));
                }
                
                // Входим пользователя в систему
                await _signInManager.SignInAsync(user, isPersistent: true);
                
                _logger.LogInformation($"Пользователь {email} успешно вошел через {info.LoginProvider}");
                
                return RedirectToLocal(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Непредвиденная ошибка при обработке внешней авторизации");
                TempData["ErrorMessage"] = "Произошла непредвиденная ошибка при обработке внешней авторизации. Пожалуйста, попробуйте снова.";
                return RedirectToAction(nameof(Login));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginViewModel model, string returnUrl = null)
        {
            _logger.LogInformation("Начало подтверждения внешней авторизации");
            
            if (ModelState.IsValid)
            {
                // Получаем информацию о входе от внешнего провайдера
                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null)
                {
                    _logger.LogError("Ошибка при загрузке информации о внешнем входе во время подтверждения");
                    throw new ApplicationException("Ошибка при загрузке информации о внешнем входе во время подтверждения.");
                }
                
                _logger.LogInformation($"Получена информация о провайдере: {info.LoginProvider}, Key: {info.ProviderKey}");
                
                // Проверяем существование пользователя по email
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                
                if (existingUser != null)
                {
                    _logger.LogInformation($"Найден существующий пользователь с email {model.Email}");
                    
                    // Проверяем, существует ли уже связь с провайдером
                    var existingLogin = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                    
                    if (existingLogin == null)
                    {
                        _logger.LogInformation("Добавляем внешний логин к существующему пользователю");
                        // Связываем существующего пользователя с внешним провайдером
                        var addLoginResult = await _userManager.AddLoginAsync(existingUser, info);
                        
                        if (addLoginResult.Succeeded)
                        {
                            _logger.LogInformation("Успешно добавлен внешний логин к существующему пользователю");
                            await _signInManager.SignInAsync(existingUser, isPersistent: false);
                            return RedirectToLocal(returnUrl);
                        }
                        
                        AddErrors(addLoginResult);
                    }
                }
                
                var user = new ApplicationUser 
                { 
                    UserName = model.Email, 
                    Email = model.Email,
                    RegistrationDate = DateTime.UtcNow,
                    EmailConfirmed = true
                };
                
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation($"Успешно создан новый пользователь с email {model.Email}");
                    
                    // Создаем профиль пользователя
                    var profile = new UserProfile
                    {
                        UserId = user.Id,
                        DisplayName = model.DisplayName ?? model.Email.Split('@')[0]
                    };
                    
                    // Получаем доступ к DbContext
                    var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
                    dbContext.UserProfiles.Add(profile);
                    await dbContext.SaveChangesAsync();
                    
                    _logger.LogInformation("Создан профиль пользователя");

                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Успешно добавлен внешний логин к новому пользователю");
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return RedirectToLocal(returnUrl);
                    }
                    else
                    {
                        _logger.LogError($"Ошибка при добавлении внешнего логина: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
                else
                {
                    _logger.LogError($"Ошибка при создании пользователя: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                
                AddErrors(result);
            }
            else
            {
                _logger.LogWarning("Модель невалидна: " + string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)));
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyProfile()
        {
            // Получаем ID текущего пользователя
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                // Если ID не найден, перенаправляем на вход
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login");
            }
            
            _logger.LogInformation($"Запрос MyProfile для userId={userId}");
            
            // Получаем доступ к DbContext
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
            
            try
            {
                // Получаем профиль напрямую через SQL для гарантии свежих данных
                var profileSql = @"
                    SELECT * FROM user_profiles 
                    WHERE user_id = @userId 
                    LIMIT 1";
                
                var profiles = await dbContext.UserProfiles
                    .FromSqlRaw(profileSql, new NpgsqlParameter("userId", userId))
                    .ToListAsync();
                
                var profile = profiles.FirstOrDefault();
                
                // Если профиль найден и имеет имя, перенаправляем на страницу профиля по имени
                if (profile != null && !string.IsNullOrEmpty(profile.DisplayName))
                {
                    _logger.LogInformation($"Найден профиль, перенаправление на ProfileByName: DisplayName={profile.DisplayName}");
                    return RedirectToAction("ProfileByName", new { username = profile.DisplayName });
                }
                
                // Иначе перенаправляем на страницу профиля по ID
                _logger.LogInformation($"Профиль не найден или без DisplayName, перенаправление на Profile: UserId={userId}");
                return RedirectToAction("Profile", new { userId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при перенаправлении на профиль: {ex.Message}");
                return RedirectToAction("Profile", new { userId = userId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Profile(string userId)
        {
            // Проверка валидности ID
            if (string.IsNullOrEmpty(userId))
            {
                return NotFound();
            }

            // Поиск пользователя по ID
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Получаем доступ к DbContext для явной загрузки профиля
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
            
            // Загружаем профиль напрямую из базы данных
            var profile = await dbContext.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            // Создаем модель для представления
            var model = new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                DisplayName = profile?.DisplayName ?? "Пользователь", // Используем значение из профиля или дефолтное
                Bio = profile?.Bio,
                AvatarUrl = profile?.AvatarUrl,
                WebsiteUrl = profile?.WebsiteUrl,
                Location = profile?.Location,
                RegistrationDate = user.RegistrationDate,
                // Проверяем, является ли просматриваемый профиль профилем текущего пользователя
                IsCurrentUser = User.Identity.IsAuthenticated && User.FindFirstValue(ClaimTypes.NameIdentifier) == userId
            };

            // Если это профиль текущего пользователя и DisplayName не задан, перенаправляем на редактирование профиля
            if (model.IsCurrentUser && string.IsNullOrEmpty(model.DisplayName))
            {
                TempData["StatusMessage"] = "Пожалуйста, заполните информацию о вашем профиле";
                return RedirectToAction("EditProfile");
            }

            return View("Profile", model);
        }
        
        [HttpGet]
        [Route("user/{username}")]
        public async Task<IActionResult> ProfileByName(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                _logger.LogWarning("Профиль не найден: пустое имя пользователя");
                return NotFound();
            }
            
            _logger.LogInformation($"Поиск профиля: username={username}");
            
            // Получаем доступ к DbContext
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
            
            try 
            {
                // Используем прямой SQL-запрос для получения профиля
                var profileSql = @"
                    SELECT * FROM user_profiles 
                    WHERE display_name = @displayName 
                    LIMIT 1";
                
                var profiles = await dbContext.UserProfiles
                    .FromSqlRaw(profileSql, new NpgsqlParameter("displayName", username))
                    .ToListAsync();
                
                var profile = profiles.FirstOrDefault();
                
                if (profile == null)
                {
                    _logger.LogWarning($"Профиль не найден: username={username}");
                    
                    // Ищем похожие профили для подсказки пользователю
                    var similarSql = @"
                        SELECT * FROM user_profiles 
                        WHERE display_name ILIKE @pattern 
                        LIMIT 5";
                    
                    var similarProfiles = await dbContext.UserProfiles
                        .FromSqlRaw(similarSql, new NpgsqlParameter("pattern", $"%{username}%"))
                        .ToListAsync();
                    
                    if (similarProfiles.Any())
                    {
                        _logger.LogInformation($"Найдены похожие профили: {similarProfiles.Count}");
                        TempData["SimilarProfiles"] = JsonSerializer.Serialize(
                            similarProfiles.Select(p => new { p.DisplayName }).ToList());
                    }
                    
                    return NotFound();
                }
                
                _logger.LogInformation($"Профиль найден: UserId={profile.UserId}, DisplayName={profile.DisplayName}");
                
                // Получаем пользователя из Identity
                var user = await _userManager.FindByIdAsync(profile.UserId);
                if (user == null)
                {
                    _logger.LogWarning($"Пользователь не найден: UserId={profile.UserId}");
                    return NotFound();
                }
                
                // Создаем модель для представления
                var model = new ProfileViewModel
                {
                    UserId = profile.UserId,
                    Email = user.Email,
                    DisplayName = profile.DisplayName,
                    Bio = profile.Bio,
                    AvatarUrl = profile.AvatarUrl,
                    WebsiteUrl = profile.WebsiteUrl,
                    Location = profile.Location,
                    RegistrationDate = user.RegistrationDate,
                    IsCurrentUser = User.Identity.IsAuthenticated && 
                                    User.FindFirstValue(ClaimTypes.NameIdentifier) == profile.UserId
                };
                
                // Если это текущий пользователь, обновляем куки для синхронизации UI
                if (model.IsCurrentUser)
                {
                    // Обновляем куки имени пользователя, если оно не совпадает с текущим в сессии
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser?.Profile?.DisplayName != profile.DisplayName)
                    {
                        Response.Cookies.Append("UserDisplayName", profile.DisplayName, new CookieOptions
                        {
                            Expires = DateTimeOffset.Now.AddMinutes(5),
                            HttpOnly = false,
                            SameSite = SameSiteMode.Lax,
                            IsEssential = true,
                            Secure = Request.IsHttps
                        });
                    }
                }
                
                return View("Profile", model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении профиля: {ex.Message}");
                return NotFound();
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
        {
            // Базовая валидация
            if (string.IsNullOrWhiteSpace(model.DisplayName))
            {
                ModelState.AddModelError("DisplayName", "Имя пользователя не может быть пустым");
                return View("EditProfile", model);
            }

            // Получаем текущего пользователя
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Login");
            }

            // Получаем доступ к DbContext
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
            
            try
            {
                // 1. Проверяем уникальность имени пользователя напрямую через SQL
                var checkNameSql = @"
                    SELECT COUNT(*) 
                    FROM user_profiles 
                    WHERE display_name = @displayName AND user_id != @userId";
                
                var nameCount = await dbContext.Database.ExecuteSqlRawAsync(checkNameSql,
                    new NpgsqlParameter("displayName", model.DisplayName),
                    new NpgsqlParameter("userId", userId));
                
                if (nameCount > 0)
                {
                    ModelState.AddModelError("DisplayName", "Это имя пользователя уже занято. Пожалуйста, выберите другое.");
                    return View("EditProfile", model);
                }

                // 2. Получаем старое имя для логирования и сравнения
                string oldDisplayName = null;
                var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                
                if (profile != null)
                {
                    oldDisplayName = profile.DisplayName;
                }

                // Фиксируем, изменилось ли имя
                bool displayNameChanged = oldDisplayName != model.DisplayName;
                _logger.LogInformation($"Profile update: UserId={userId}, OldName={oldDisplayName}, NewName={model.DisplayName}, Changed={displayNameChanged}");

                // 3. Напрямую обновляем профиль через SQL - самый надежный способ
                var updateSql = @"
                    INSERT INTO user_profiles (user_id, display_name, bio, avatar_url, website_url, location)
                    VALUES (@userId, @displayName, @bio, @avatarUrl, @websiteUrl, @location)
                    ON CONFLICT (user_id) 
                    DO UPDATE SET 
                        display_name = @displayName,
                        bio = @bio, 
                        avatar_url = @avatarUrl,
                        website_url = @websiteUrl,
                        location = @location";
                
                await dbContext.Database.ExecuteSqlRawAsync(updateSql,
                    new NpgsqlParameter("userId", userId),
                    new NpgsqlParameter("displayName", model.DisplayName),
                    new NpgsqlParameter("bio", model.Bio ?? (object)DBNull.Value),
                    new NpgsqlParameter("avatarUrl", model.AvatarUrl ?? (object)DBNull.Value),
                    new NpgsqlParameter("websiteUrl", model.WebsiteUrl ?? (object)DBNull.Value),
                    new NpgsqlParameter("location", model.Location ?? (object)DBNull.Value));
                
                // 4. Проверяем результат обновления через прямой SQL, а не через Entity Framework
                bool updateSuccessful = true;
                try 
                {
                    var checkSql = @"SELECT display_name FROM user_profiles WHERE user_id = @userId";
                    var checkResult = await dbContext.Database.SqlQueryRaw<string>(checkSql, 
                        new NpgsqlParameter("userId", userId))
                        .ToListAsync();
                    
                    var actualName = checkResult.FirstOrDefault();
                    _logger.LogInformation($"Прямая проверка SQL: UserId={userId}, ActualName={actualName}, RequestedName={model.DisplayName}");
                    
                    // Не выбрасываем исключение, даже если имена кажутся разными из-за кэширования
                    if (actualName != model.DisplayName)
                    {
                        _logger.LogWarning($"Имя кажется не обновленным, но мы продолжаем: UserId={userId}, ActualName={actualName}, RequestedName={model.DisplayName}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Ошибка при проверке имени (игнорируем): {ex.Message}");
                }

                _logger.LogInformation($"Profile updated successfully: UserId={userId}, DisplayName={model.DisplayName}");

                // 5. Обновляем пользователя в Identity
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    // Обновляем навигационное свойство при наличии
                    if (user.Profile != null)
                    {
                        user.Profile.DisplayName = model.DisplayName;
                    }
                    await _userManager.UpdateAsync(user);
                }

                // 6. Если имя изменилось, делаем полный сброс сессии
                if (displayNameChanged)
                {
                    // Сохраняем новое имя в TempData и куки для передачи между запросами
                    TempData["StatusMessage"] = "Ваш профиль успешно обновлен. Имя пользователя изменено.";
                    TempData["DisplayNameChanged"] = "true";
                    TempData["NewDisplayName"] = model.DisplayName;
                    
                    // Cookie для JavaScript
                    Response.Cookies.Append("UserDisplayName", model.DisplayName, new CookieOptions
                    { 
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Обновляем и в localStorage через JavaScript
                    TempData["UpdateLocalStorage"] = "true";
                    TempData["UserDisplayNameValue"] = model.DisplayName;
                    
                    try 
                    {
                        // Делаем полный выход и вход заново
                        await _signInManager.SignOutAsync();
                        
                        // Добавляем небольшую задержку для гарантированного обновления сессии
                        await Task.Delay(300);
                        
                        // Входим заново, используя сохраненный объект пользователя
                        await _signInManager.SignInAsync(user, isPersistent: true);
                        
                        // Еще небольшая задержка для стабилизации сессии
                        await Task.Delay(200);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обновлении сессии, но продолжаем работу");
                        // Не прерываем выполнение - даже если обновление сессии не удалось,
                        // после перезагрузки страницы все должно быть в порядке
                    }
                    
                    // Добавляем cookie для форсирования обновления UI
                    Response.Cookies.Append("SessionRefresh", "true", new CookieOptions
                    {
                        Expires = DateTimeOffset.Now.AddMinutes(5),
                        HttpOnly = false,
                        SameSite = SameSiteMode.Lax,
                        IsEssential = true,
                        Secure = Request.IsHttps
                    });
                    
                    // Используем специальную страницу перенаправления вместо прямого редиректа
                    TempData["RedirectTo"] = Url.Action("ProfileByName", new { username = model.DisplayName });
                    return RedirectToAction("RedirectAfterLogin");
                }
                else
                {
                    // Обновляем сессию без полного сброса
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["StatusMessage"] = "Ваш профиль успешно обновлен";
                }
                
                // 7. Перенаправляем на страницу профиля
                return RedirectToAction("ProfileByName", new { username = model.DisplayName });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении профиля: {Message}", ex.Message);
                ModelState.AddModelError(string.Empty, $"Ошибка при обновлении профиля: {ex.Message}");
                
                // Загружаем текущие данные для отображения формы
                if (model.UserId == null) model.UserId = userId;
                return View("EditProfile", model);
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("Пользователь не найден при доступе к редактированию профиля. UserId: {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                
                // Выход и перенаправление на страницу входа
                await _signInManager.SignOutAsync();
                TempData["ErrorMessage"] = "Ваша сессия устарела. Пожалуйста, войдите снова.";
                return RedirectToAction("Login");
            }

            // Получаем доступ к DbContext для явной загрузки профиля
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();
            
            // Загружаем профиль напрямую из базы данных
            var profile = await dbContext.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            var model = new ProfileViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                DisplayName = profile?.DisplayName ?? "", // Если DisplayName не задан, оставляем пустую строку для заполнения
                Bio = profile?.Bio,
                AvatarUrl = profile?.AvatarUrl,
                WebsiteUrl = profile?.WebsiteUrl,
                Location = profile?.Location,
                RegistrationDate = user.RegistrationDate,
                IsCurrentUser = true
            };

            return View(model);
        }

        // Метод для поиска пользователей по имени
        [HttpGet]
        public async Task<IActionResult> FindUsers(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return BadRequest("Имя пользователя не может быть пустым");
            }

            // Получаем доступ к DbContext
            var dbContext = HttpContext.RequestServices.GetRequiredService<AppGambit.Data.ApplicationDbContext>();

            // Ищем пользователей, где DisplayName содержит заданную строку (без учета регистра)
            var users = await dbContext.UserProfiles
                .Where(p => p.DisplayName != null && EF.Functions.ILike(p.DisplayName, $"%{username}%"))
                .Take(10) // Ограничиваем результаты до 10 для производительности
                .Select(p => new 
                { 
                    UserId = p.UserId,
                    DisplayName = p.DisplayName,
                    AvatarUrl = p.AvatarUrl
                })
                .ToListAsync();

            return Json(users);
        }

        // Метод для отображения страницы поиска пользователей
        [HttpGet]
        public IActionResult SearchUsers()
        {
            return View();
        }

        [HttpGet]
        [Route("profile")]
        [Authorize]
        public IActionResult ProfileRedirect()
        {
            // Перенаправление на MyProfile, который определит правильный URL с именем пользователя
            return RedirectToAction("MyProfile");
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            _logger.LogInformation($"RedirectToLocal: {returnUrl}");
            
            // Add cookies to ensure client-side authentication state is refreshed
            Response.Cookies.Append("UserAuthenticated", "true", new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddMinutes(5),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Secure = Request.IsHttps
            });
            
            Response.Cookies.Append("SessionRefresh", "true", new CookieOptions
            {
                Expires = DateTimeOffset.Now.AddMinutes(5),
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Secure = Request.IsHttps
            });
            
            // Using RedirectAfterLogin to handle the redirect properly
            if (Url.IsLocalUrl(returnUrl))
            {
                TempData["RedirectTo"] = returnUrl;
                return RedirectToAction("RedirectAfterLogin");
            }
            else
            {
                // Всегда перенаправляем на MyProfile, который сам определит лучший URL
                TempData["RedirectTo"] = Url.Action("MyProfile", "Account");
                return RedirectToAction("RedirectAfterLogin");
            }
        }

        #endregion
    }
} 