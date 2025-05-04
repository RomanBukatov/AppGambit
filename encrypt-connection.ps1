# Run this script to generate an encrypted connection string for PostgreSQL
# This allows you to safely store the connection string in your appsettings.json file

# Build and run the encryption tool
dotnet run --project AppGambit.Security.Tools encrypt-connection

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 