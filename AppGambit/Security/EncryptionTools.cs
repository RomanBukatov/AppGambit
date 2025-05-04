using System;

namespace AppGambit.Security
{
    /// <summary>
    /// Launcher for security tools
    /// </summary>
    public static class EncryptionTools
    {
        /// <summary>
        /// Main entry point for the encryption tools
        /// </summary>
        /// <param name="args">Command line arguments</param>
        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                string tool = args[0].ToLower();
                string[] toolArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

                switch (tool)
                {
                    case "encrypt-connection":
                        GenerateEncryptedConnectionStringTool.Run(toolArgs);
                        break;

                    case "help":
                    case "--help":
                    case "-h":
                        ShowHelp();
                        break;

                    default:
                        Console.WriteLine($"Unknown tool: {tool}");
                        ShowHelp();
                        break;
                }
            }
            else
            {
                // Default to encrypted connection string tool if no arguments provided
                GenerateEncryptedConnectionStringTool.Run(Array.Empty<string>());
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("AppGambit Security Tools");
            Console.WriteLine("=======================");
            Console.WriteLine();
            Console.WriteLine("Usage: dotnet run --project AppGambit.Security.Tools [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  encrypt-connection    Generate an encrypted database connection string");
            Console.WriteLine("  help                  Show this help message");
            Console.WriteLine();
        }
    }
} 