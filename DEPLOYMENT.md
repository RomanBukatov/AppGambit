# Руководство по развертыванию AppGambit

## 🚀 Локальное развертывание

### Предварительные требования

- .NET 8.0 SDK
- PostgreSQL 12+
- Git

### Пошаговая инструкция

1. **Клонирование репозитория**
   ```bash
   git clone https://github.com/yourusername/AppGambit.git
   cd AppGambit
   ```

2. **Настройка PostgreSQL**
   
   Создайте базу данных:
   ```sql
   CREATE DATABASE appgambit;
   CREATE USER appgambit_user WITH PASSWORD 'your_password';
   GRANT ALL PRIVILEGES ON DATABASE appgambit TO appgambit_user;
   ```

3. **Конфигурация приложения**
   
   Создайте `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=appgambit;Username=appgambit_user;Password=your_password"
     },
     "Authentication": {
       "Google": {
         "ClientId": "your-google-client-id",
         "ClientSecret": "your-google-client-secret"
       }
     }
   }
   ```

4. **Установка зависимостей и миграции**
   ```bash
   dotnet restore
   dotnet ef database update
   ```

5. **Запуск приложения**
   ```bash
   dotnet run
   ```

## 🐳 Docker развертывание

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AppGambit.csproj", "."]
RUN dotnet restore "./AppGambit.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "AppGambit.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AppGambit.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AppGambit.dll"]
```

### Docker Compose

```yaml
version: '3.8'

services:
  app:
    build: .
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=db;Database=appgambit;Username=postgres;Password=postgres
    depends_on:
      - db
    volumes:
      - ./wwwroot/uploads:/app/wwwroot/uploads

  db:
    image: postgres:15
    environment:
      POSTGRES_DB: appgambit
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

volumes:
  postgres_data:
```

### Команды для запуска

```bash
# Сборка и запуск
docker-compose up --build

# Запуск в фоновом режиме
docker-compose up -d

# Остановка
docker-compose down
```

## ☁️ Облачное развертывание

### Azure App Service

1. **Создание ресурсов**
   ```bash
   # Создание группы ресурсов
   az group create --name AppGambitRG --location "East US"
   
   # Создание плана App Service
   az appservice plan create --name AppGambitPlan --resource-group AppGambitRG --sku B1 --is-linux
   
   # Создание веб-приложения
   az webapp create --resource-group AppGambitRG --plan AppGambitPlan --name appgambit-app --runtime "DOTNETCORE|8.0"
   ```

2. **Настройка базы данных**
   ```bash
   # Создание PostgreSQL сервера
   az postgres server create --resource-group AppGambitRG --name appgambit-db --location "East US" --admin-user adminuser --admin-password YourPassword123 --sku-name GP_Gen5_2
   ```

3. **Развертывание**
   ```bash
   # Публикация из Visual Studio или
   dotnet publish -c Release
   az webapp deployment source config-zip --resource-group AppGambitRG --name appgambit-app --src publish.zip
   ```

### AWS Elastic Beanstalk

1. **Подготовка**
   ```bash
   # Установка EB CLI
   pip install awsebcli
   
   # Инициализация
   eb init
   ```

2. **Создание окружения**
   ```bash
   eb create appgambit-env
   ```

3. **Развертывание**
   ```bash
   dotnet publish -c Release
   eb deploy
   ```

## 🔧 Конфигурация для продакшена

### Переменные окружения

```bash
# Строка подключения к БД
export ConnectionStrings__DefaultConnection="Host=your-db-host;Database=appgambit;Username=user;Password=pass"

# Google OAuth
export Authentication__Google__ClientId="your-client-id"
export Authentication__Google__ClientSecret="your-client-secret"

# Настройки логирования
export Logging__LogLevel__Default="Warning"
export Logging__LogLevel__Microsoft="Warning"
```

### appsettings.Production.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=production-db;Database=appgambit;Username=prod_user;Password=secure_password"
  }
}
```

## 🔒 Безопасность

### SSL/TLS

```csharp
// В Program.cs для продакшена
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
```

### Секреты

Используйте Azure Key Vault, AWS Secrets Manager или аналогичные сервисы для хранения секретов.

### Настройки безопасности

```json
{
  "SecurityHeaders": {
    "ContentSecurityPolicy": "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'",
    "XFrameOptions": "DENY",
    "XContentTypeOptions": "nosniff"
  }
}
```

## 📊 Мониторинг

### Application Insights (Azure)

```csharp
// В Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### Логирование

```csharp
// Настройка Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));
```

## 🔄 CI/CD

### GitHub Actions

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 8.0.x
        
    - name: Restore dependencies
      run: dotnet restore
      
    - name: Build
      run: dotnet build --no-restore
      
    - name: Test
      run: dotnet test --no-build --verbosity normal
      
    - name: Publish
      run: dotnet publish -c Release -o ./publish
      
    - name: Deploy to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: 'appgambit-app'
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: './publish'
```

## 🗄️ Резервное копирование

### PostgreSQL

```bash
# Создание бэкапа
pg_dump -h hostname -U username -d appgambit > backup.sql

# Восстановление
psql -h hostname -U username -d appgambit < backup.sql
```

### Файлы

```bash
# Бэкап загруженных файлов
tar -czf uploads-backup.tar.gz wwwroot/uploads/
```

## 🚨 Устранение неполадок

### Частые проблемы

1. **Ошибка подключения к БД**
   - Проверьте строку подключения
   - Убедитесь, что PostgreSQL запущен
   - Проверьте права доступа пользователя

2. **Ошибки миграций**
   ```bash
   # Сброс миграций
   dotnet ef database drop
   dotnet ef database update
   ```

3. **Проблемы с файлами**
   - Проверьте права доступа к папке uploads
   - Убедитесь в наличии свободного места

### Логи

```bash
# Просмотр логов в Docker
docker-compose logs app

# Логи в Azure
az webapp log tail --name appgambit-app --resource-group AppGambitRG
```

## 📞 Поддержка

При возникновении проблем:
1. Проверьте логи приложения
2. Убедитесь в правильности конфигурации
3. Создайте issue в GitHub репозитории