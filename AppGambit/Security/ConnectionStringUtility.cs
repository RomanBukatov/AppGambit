using System;
using System.Text;

namespace AppGambit.Security
{
    /// <summary>
    /// Utility class for generating encrypted connection strings
    /// </summary>
    public static class ConnectionStringUtility
    {
        /// <summary>
        /// Generates a PostgreSQL connection string and encrypts it
        /// </summary>
        /// <param name="host">Database server host</param>
        /// <param name="port">PostgreSQL port (usually 5432)</param>
        /// <param name="database">Database name</param>
        /// <param name="username">Database username</param>
        /// <param name="password">Database password</param>
        /// <returns>Encrypted connection string that can be stored in appsettings.json</returns>
        public static string GenerateEncryptedConnectionString(
            string host, 
            int port, 
            string database, 
            string username, 
            string password)
        {
            // Construct the connection string
            var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
            
            // Encrypt the connection string
            return DbConnectionEncryption.EncryptConnectionString(connectionString);
        }
    }
} 