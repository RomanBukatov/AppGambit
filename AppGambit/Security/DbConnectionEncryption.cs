using System;
using System.Security.Cryptography;
using System.Text;

namespace AppGambit.Security
{
    /// <summary>
    /// Provides methods for encrypting and decrypting database connection strings
    /// </summary>
    public static class DbConnectionEncryption
    {
        // ВАЖНО: В реальном приложении ключи должны храниться в безопасном месте (Azure Key Vault, AWS KMS и т.д.)
        // Этот ключ используется только для демонстрации
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("YourSecretKey12345678901234567890");
        private static readonly byte[] Iv = Encoding.UTF8.GetBytes("1234567890123456");

        /// <summary>
        /// Encrypts a database connection string
        /// </summary>
        /// <param name="connectionString">The connection string to encrypt</param>
        /// <returns>Base64 encoded encrypted connection string</returns>
        public static string EncryptConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = Iv;

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                byte[] encryptedBytes;
                using (var ms = new System.IO.MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (var sw = new System.IO.StreamWriter(cs))
                        {
                            sw.Write(connectionString);
                        }
                        encryptedBytes = ms.ToArray();
                    }
                }

                return Convert.ToBase64String(encryptedBytes);
            }
        }

        /// <summary>
        /// Decrypts an encrypted database connection string
        /// </summary>
        /// <param name="encryptedConnectionString">Base64 encoded encrypted connection string</param>
        /// <returns>Decrypted connection string</returns>
        public static string DecryptConnectionString(string encryptedConnectionString)
        {
            if (string.IsNullOrEmpty(encryptedConnectionString))
                throw new ArgumentException("Encrypted connection string cannot be null or empty", nameof(encryptedConnectionString));

            byte[] cipherBytes = Convert.FromBase64String(encryptedConnectionString);

            using (Aes aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = Iv;

                ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var ms = new System.IO.MemoryStream(cipherBytes))
                {
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (var sr = new System.IO.StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
} 