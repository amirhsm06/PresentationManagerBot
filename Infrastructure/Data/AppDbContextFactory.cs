using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PresentationManagerBot.Infrastructure.Data;

public class AppDbContextFactory
    : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var appSettingsPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "appsettings.json");

        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException(
                "appsettings.json was not found.",
                appSettingsPath);
        }

        var json = File.ReadAllText(appSettingsPath);

        using var document =
            JsonDocument.Parse(json);

        var connectionString =
            document.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("DefaultConnection")
                .GetString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is missing.");
        }

        var options =
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

        return new AppDbContext(options);
    }
}