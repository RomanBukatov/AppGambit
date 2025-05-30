# AppGambit 🚀

Современная платформа для размещения и скачивания приложений, построенная на ASP.NET Core 8.0 с использованием PostgreSQL.

## ✨ Особенности

- 🔐 **Аутентификация и авторизация** - регистрация, вход, Google OAuth
- 📱 **Управление приложениями** - загрузка, описание, скриншоты, категории
- ⭐ **Система рейтингов** - оценка приложений звездами и лайки/дизлайки
- 💬 **Комментарии** - обсуждение приложений с возможностью редактирования
- 👤 **Профили пользователей** - красивые URL `/u/username`, статистика
- 🔍 **Умный поиск** - поиск приложений и пользователей
- 🏷️ **Теги** - категоризация и фильтрация приложений
- 📊 **Статистика** - скачивания, рейтинги, активность пользователей
- 📱 **Адаптивный дизайн** - работает на всех устройствах

## 🛠️ Технологии

- **Backend**: ASP.NET Core 8.0, Entity Framework Core
- **Database**: PostgreSQL
- **Frontend**: Bootstrap 5, Font Awesome, JavaScript
- **Authentication**: ASP.NET Core Identity, Google OAuth
- **File Storage**: Local file system с поддержкой изображений

## 🚀 Быстрый старт

### Предварительные требования

- .NET 8.0 SDK
- PostgreSQL 12+
- Visual Studio 2022 или VS Code

### Установка

1. **Клонируйте репозиторий**
   ```bash
   git clone https://github.com/yourusername/AppGambit.git
   cd AppGambit
   ```

2. **Настройте базу данных**
   
   Создайте базу данных PostgreSQL и обновите строку подключения в `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=appgambit;Username=your_username;Password=your_password"
     }
   }
   ```

3. **Настройте Google OAuth (опционально)**
   
   Добавьте в `appsettings.json`:
   ```json
   {
     "Authentication": {
       "Google": {
         "ClientId": "your-google-client-id",
         "ClientSecret": "your-google-client-secret"
       }
     }
   }
   ```

4. **Примените миграции**
   ```bash
   dotnet ef database update
   ```

5. **Запустите приложение**
   ```bash
   dotnet run
   ```

6. **Откройте браузер**
   
   Перейдите на `https://localhost:5001` или `http://localhost:5000`

## 📁 Структура проекта

```
AppGambit/
├── Controllers/          # MVC контроллеры
├── Data/                # Контекст базы данных
├── Migrations/          # Миграции Entity Framework
├── Models/              # Модели данных
├── Services/            # Бизнес-логика и сервисы
├── ViewModels/          # Модели представлений
├── Views/               # Razor представления
├── wwwroot/             # Статические файлы
│   ├── css/            # Стили
│   ├── js/             # JavaScript
│   └── uploads/        # Загруженные файлы
└── Program.cs           # Точка входа приложения
```

## 🎯 Основные функции

### Для пользователей
- Регистрация и вход в систему
- Загрузка приложений с описанием и скриншотами
- Оценка и комментирование приложений
- Поиск приложений по названию, описанию, тегам
- Просмотр профилей других пользователей

### Для разработчиков
- Чистая архитектура с разделением ответственности
- Repository pattern для работы с данными
- Dependency Injection
- Async/await для асинхронных операций
- Валидация данных на клиенте и сервере

## 🔧 Конфигурация

### Настройки приложения

Основные настройки находятся в `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=appgambit;Username=postgres;Password=password"
  },
  "Authentication": {
    "Google": {
      "ClientId": "your-client-id",
      "ClientSecret": "your-client-secret"
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

### Переменные окружения

Для продакшена рекомендуется использовать переменные окружения:

- `ConnectionStrings__DefaultConnection` - строка подключения к БД
- `Authentication__Google__ClientId` - Google OAuth Client ID
- `Authentication__Google__ClientSecret` - Google OAuth Client Secret

## 📝 API Endpoints

### Приложения
- `GET /` - Главная страница
- `GET /Applications` - Список приложений
- `GET /Applications/Details/{id}` - Детали приложения
- `POST /Applications/Create` - Создание приложения
- `POST /Applications/Rate` - Оценка приложения
- `POST /Applications/AddComment` - Добавление комментария

### Профили
- `GET /u/{username}` - Профиль пользователя
- `GET /Profile/Search` - Поиск пользователей

### Поиск
- `GET /Applications/Search?search={query}` - Глобальный поиск

## 🎨 Кастомизация

### Темы и стили

Основные стили находятся в `wwwroot/css/site.css`. Проект использует:
- Bootstrap 5 для базовых компонентов
- Font Awesome для иконок
- Кастомные CSS переменные для цветов
- Адаптивный дизайн с медиа-запросами

### Добавление новых функций

1. Создайте модель в папке `Models/`
2. Добавьте миграцию: `dotnet ef migrations add YourMigrationName`
3. Создайте контроллер в папке `Controllers/`
4. Добавьте представления в папку `Views/`

## 🧪 Тестирование

```bash
# Запуск тестов
dotnet test

# Проверка покрытия кода
dotnet test --collect:"XPlat Code Coverage"
```

## 📦 Развертывание

### Docker

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

### Azure/AWS

Проект готов для развертывания на облачных платформах с минимальными изменениями конфигурации.

## 🤝 Вклад в проект

1. Форкните репозиторий
2. Создайте ветку для новой функции (`git checkout -b feature/AmazingFeature`)
3. Зафиксируйте изменения (`git commit -m 'Add some AmazingFeature'`)
4. Отправьте в ветку (`git push origin feature/AmazingFeature`)
5. Откройте Pull Request

## 📄 Лицензия

Этот проект распространяется под лицензией MIT. См. файл `LICENSE` для подробностей.

## 👥 Авторы

- **Ваше имя** - *Основной разработчик* - [YourGitHub](https://github.com/yourusername)

## 🙏 Благодарности

- ASP.NET Core команде за отличный фреймворк
- Bootstrap за UI компоненты
- Font Awesome за иконки
- Сообществу .NET за поддержку и вдохновение

---

⭐ Поставьте звезду, если проект вам понравился!