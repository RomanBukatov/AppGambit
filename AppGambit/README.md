# AppGambit - Software Catalog Application

A web application for cataloging and managing software downloads built with ASP.NET Core and PostgreSQL.

## Database Setup

This application uses PostgreSQL as its database. Follow these steps to set up the database:

1. Install PostgreSQL on your system if you haven't already.

2. Configure the database connection securely using one of the following methods:

   ### Development Environment - Using User Secrets

   User Secrets allows storing sensitive configuration outside your project files:

   ```bash
   # Initialize user secrets (if not already done)
   dotnet user-secrets init --project AppGambit

   # Set the PostgreSQL connection string in user secrets
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=appgambit;Username=postgres;Password=your_password" --project AppGambit
   ```

   ### Development Environment - Using appsettings.Development.json

   Alternatively, you can use the `appsettings.Development.json` file which is excluded from git:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=appgambit;Username=postgres;Password=your_password"
     }
   }
   ```

   ### Production Environment - Using Environment Variables

   For production, use environment variables to store the connection string:

   **Windows (PowerShell):**
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Host=your-server;Port=5432;Database=appgambit;Username=postgres;Password=your_secure_password"
   ```

   **Linux/macOS:**
   ```bash
   export ConnectionStrings__DefaultConnection="Host=your-server;Port=5432;Database=appgambit;Username=postgres;Password=your_secure_password"
   ```

   **Docker:**
   ```
   docker run -e ConnectionStrings__DefaultConnection="Host=your-server;Port=5432;Database=appgambit;Username=postgres;Password=your_secure_password" your-image
   ```

   **IIS (Web.config):**
   ```xml
   <configuration>
     <location path="." inheritInChildApplications="false">
       <system.webServer>
         <aspNetCore processPath="dotnet" ...>
           <environmentVariables>
             <environmentVariable name="ConnectionStrings__DefaultConnection" 
               value="Host=your-server;Port=5432;Database=appgambit;Username=postgres;Password=your_secure_password" />
           </environmentVariables>
         </aspNetCore>
       </system.webServer>
     </location>
   </configuration>
   ```

3. Run database migrations to create the database schema:
   - Open a command prompt in the project directory
   
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate --project AppGambit
   dotnet ef database update --project AppGambit
   ```

## Database Schema

The database schema follows the design specified in the requirements, with the following tables:

### Identity Tables
- AspNetUsers - User accounts with additional fields (registration_date, last_login_date, is_active)
- AspNetRoles - User roles
- AspNetUserRoles - User-role associations
- AspNetUserClaims - User claims
- AspNetRoleClaims - Role claims
- AspNetUserLogins - External login providers
- AspNetUserTokens - User tokens

### Application Tables
- user_profiles - User profile information with display name, avatar, bio
- categories - Software categories with hierarchical structure
- programs - Software entries with descriptions, versions, download links
- screenshots - Images for programs
- tags - Keywords for programs
- program_tags - Many-to-many relationship between programs and tags
- comments - User comments on programs with hierarchical replies
- ratings - User ratings (likes/dislikes) for programs and comments
- downloads - Software download tracking
- notifications - User notifications
- program_subscriptions - User subscriptions to programs
- about_creator_info - Site information

## Running the Application

1. Make sure you have .NET 8.0 SDK installed
2. Configure the database connection securely as described above
3. Run database migrations as described above
4. Start the application with:
   ```
   dotnet run
   ```
5. Navigate to `https://localhost:5001` in your browser

## Security Considerations

For more information about secure connection string management, see:
- [User Secrets in ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [Safe storage of app secrets in development in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) 

## Конфигурация приложения

В проекте используются следующие файлы конфигурации:

1. **appsettings.json** - базовая конфигурация с плейсхолдерами для секретов. Этот файл должен быть добавлен в систему контроля версий.

2. **appsettings.Development.json** - локальная конфигурация для разработки с фактическими параметрами подключения. Этот файл **не должен** добавляться в систему контроля версий, так как он содержит секретные данные.

### Чувствительные данные

Для хранения чувствительных данных рекомендуется использовать:

- **Для разработки**: User Secrets или appsettings.Development.json
- **Для продакшн**: Переменные окружения или внешние хранилища секретов

### Структура конфигурации

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=appgambit_db;Username=postgres;Password=your_password"
  },
  "Authentication": {
    "Google": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
    },
    "Yandex": {
      "ClientId": "YOUR_YANDEX_CLIENT_ID",
      "ClientSecret": "YOUR_YANDEX_CLIENT_SECRET"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
``` 