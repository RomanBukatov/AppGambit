using AppGambit.Data;
using AppGambit.Models;
using AppGambit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure connection string - supporting multiple security methods
string connectionString;

// Check if encrypted connection string is available
var encryptedConnectionString = builder.Configuration["ConnectionStrings:EncryptedDefaultConnection"];

if (!string.IsNullOrEmpty(encryptedConnectionString)) 
{
    // Use encrypted connection string (for extra security)
    try 
    {
        connectionString = DbConnectionEncryption.DecryptConnectionString(encryptedConnectionString);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Failed to decrypt database connection string. " +
            "Ensure the encryption key is correctly configured.", ex);
    }
}
else 
{
    // Use regular connection string from user secrets or environment variables
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    if (string.IsNullOrEmpty(connectionString) || 
        connectionString.Contains("Moved to user secrets or environment variables for security"))
    {
        throw new InvalidOperationException(
            "Database connection string is not configured. " +
            "Please configure the connection string using user secrets or environment variables. " +
            "See README.md for instructions.");
    }
}

// Add database context with PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => 
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add services to the container
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
