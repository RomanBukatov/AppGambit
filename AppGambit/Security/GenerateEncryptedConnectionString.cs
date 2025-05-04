using System;
using System.Text;

namespace AppGambit.Security
{
    /// <summary>
    /// Console application for generating encrypted connection strings
    /// </summary>
    public static class GenerateEncryptedConnectionStringTool
    {
        public static void Run(string[] args)
        {
            Console.WriteLine("===============================================");
            Console.WriteLine("   PostgreSQL Connection String Encryptor Tool");
            Console.WriteLine("===============================================");
            Console.WriteLine();
            Console.WriteLine("This tool will help you create an encrypted connection string");
            Console.WriteLine("that can be safely stored in your appsettings.json file.");
            Console.WriteLine();

            // Get connection parameters from user input
            Console.Write("Host [localhost]: ");
            string host = ReadInput("localhost");

            Console.Write("Port [5432]: ");
            int port = int.Parse(ReadInput("5432"));

            Console.Write("Database name [appgambit]: ");
            string database = ReadInput("appgambit");

            Console.Write("Username [postgres]: ");
            string username = ReadInput("postgres");

            Console.Write("Password: ");
            string password = ReadPassword();

            Console.WriteLine("\nGenerating encrypted connection string...");

            try
            {
                // Generate the encrypted connection string
                string encryptedConnectionString = ConnectionStringUtility.GenerateEncryptedConnectionString(
                    host, port, database, username, password);

                Console.WriteLine("\nEncrypted Connection String:");
                Console.WriteLine(encryptedConnectionString);

                Console.WriteLine("\nAdd this to your appsettings.json file like this:");
                Console.WriteLine("{");
                Console.WriteLine("  \"ConnectionStrings\": {");
                Console.WriteLine($"    \"EncryptedDefaultConnection\": \"{encryptedConnectionString}\"");
                Console.WriteLine("  },");
                Console.WriteLine("  ...");
                Console.WriteLine("}");

                // Security warning
                Console.WriteLine("\n⚠️ IMPORTANT SECURITY NOTICE ⚠️");
                Console.WriteLine("The encryption is only as secure as your encryption key management.");
                Console.WriteLine("For production environments, use proper key management solutions");
                Console.WriteLine("such as Azure Key Vault, AWS KMS, or similar services.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
        }

        private static string ReadInput(string defaultValue)
        {
            string input = Console.ReadLine() ?? string.Empty;
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
        }

        private static string ReadPassword()
        {
            var password = new StringBuilder();
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                // Backspace handling
                if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
                // Adding characters to password
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            } while (key.Key != ConsoleKey.Enter);

            return password.ToString();
        }
    }
} 