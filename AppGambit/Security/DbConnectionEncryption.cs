using System.Security.Cryptography;
using System.Text;

namespace AppGambit.Security
{
    /// <summary>
    /// Utility class for encrypting and decrypting database connection strings.
    /// This can be used for scenarios where you need to store the connection string in a config file,
    /// but want to provide an additional layer of security.
    /// </summary>
    public static class DbConnectionEncryption
    {
        // WARNING: In a real-world application, this key should be properly managed
        // (e.g., stored in Key Vault, environment variables, etc.)
        // This is just a demo implementation to show the concept
        private static readonly byte[] EncryptionKey = new byte[32] { 
            72, 158, 214, 179, 111, 23, 89, 109, 
            42, 5, 167, 80, 151, 236, 170, 211, 
            53, 178, 233, 3, 172, 10, 198, 241, 
            77, 237, 108, 197, 48, 64, 19, 152 
        };
        private static readonly byte[] IV = new byte[16] { 
            19, 57, 121, 47, 83, 74, 33, 185, 
            92, 44, 134, 78, 173, 12, 99, 43 
        };

        /// <summary>
        /// Encrypts a connection string 
        /// </summary>
        /// <param name="connectionString">The connection string to encrypt</param>
        /// <returns>Base64 encoded encrypted string</returns>
        public static string EncryptConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
            using (var streamWriter = new StreamWriter(cryptoStream))
            {
                streamWriter.Write(connectionString);
            }

            var encryptedBytes = memoryStream.ToArray();
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Decrypts an encrypted connection string
        /// </summary>
        /// <param name="encryptedConnectionString">Base64 encoded encrypted connection string</param>
        /// <returns>Decrypted connection string</returns>
        public static string DecryptConnectionString(string encryptedConnectionString)
        {
            if (string.IsNullOrEmpty(encryptedConnectionString))
                throw new ArgumentException("Encrypted connection string cannot be null or empty", nameof(encryptedConnectionString));

            var encryptedBytes = Convert.FromBase64String(encryptedConnectionString);

            using var aes = Aes.Create();
            aes.Key = EncryptionKey;
            aes.IV = IV;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var memoryStream = new MemoryStream(encryptedBytes);
            using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
            using var streamReader = new StreamReader(cryptoStream);
            
            return streamReader.ReadToEnd();
        }
    }
} 