# Безопасное хранение строки подключения

В этом документе описаны дополнительные методы защиты строки подключения к базе данных.

## Использование шифрования строки подключения

В дополнение к стандартным методам (User Secrets и переменные окружения), вы можете использовать предоставленный класс `DbConnectionEncryption` для шифрования строки подключения, если требуется её хранение в файле конфигурации.

### Шифрование строки подключения

```csharp
// Шифруем строку подключения
string connectionString = "Host=localhost;Port=5432;Database=appgambit;Username=postgres;Password=your_password";
string encryptedConnectionString = DbConnectionEncryption.EncryptConnectionString(connectionString);

// Теперь можно сохранить зашифрованную строку в файл конфигурации
// Пример: "ConnectionStrings:EncryptedDefaultConnection": "AxB8f2Et6A7W4..."
```

### Дешифрование строки подключения

```csharp
// Получаем зашифрованную строку из конфигурации
string encryptedConnectionString = Configuration["ConnectionStrings:EncryptedDefaultConnection"];

// Дешифруем строку перед использованием
string decryptedConnectionString = DbConnectionEncryption.DecryptConnectionString(encryptedConnectionString);

// Используем расшифрованную строку подключения
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(decryptedConnectionString));
```

### Управление ключами шифрования

В производственной среде ключи шифрования должны быть защищены и управляться должным образом:

1. Не храните ключи в исходном коде
2. Используйте Azure Key Vault, AWS KMS или другие сервисы управления ключами
3. Периодически меняйте ключи
4. Обеспечьте резервное копирование ключей

## Пример использования зашифрованной строки подключения в Program.cs

```csharp
string connectionString;
var encryptedConnectionString = builder.Configuration["ConnectionStrings:EncryptedDefaultConnection"];

if (!string.IsNullOrEmpty(encryptedConnectionString))
{
    // Используем зашифрованную строку подключения
    connectionString = DbConnectionEncryption.DecryptConnectionString(encryptedConnectionString);
}
else
{
    // Используем обычную строку подключения
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

// Используем расшифрованную строку подключения
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
```

## Важно

Реализация шифрования в классе `DbConnectionEncryption` предназначена только для демонстрационных целей. В реальном приложении:

1. Не используйте захардкоженные ключи шифрования
2. Храните ключи в безопасном месте (например, Azure Key Vault)
3. Рассмотрите возможность использования асимметричного шифрования
4. Запретите сохранение ключей в системе контроля версий
5. Добавьте проверку целостности зашифрованных данных 