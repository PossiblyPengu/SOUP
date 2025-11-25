using System;
using System.IO;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using BusinessToolsSuite.Infrastructure.Data;
using BusinessToolsSuite.Infrastructure.Repositories;
using BusinessToolsSuite.Infrastructure.Services;
using BusinessToolsSuite.Infrastructure.Services.Parsers;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Features.ExpireWise.ViewModels;
using BusinessToolsSuite.Shared.Services;

namespace BusinessToolsSuite.Desktop;

sealed class Program
{
    public static IHost? AppHost { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Serilog
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appDataPath, "ExpireWise", "Logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Starting ExpireWise");

            // Build host for dependency injection
            AppHost = BuildHost();

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
            AppHost?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Database
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dbDir = Path.Combine(appDataPath, "ExpireWise");
                Directory.CreateDirectory(dbDir);
                var dbPath = Path.Combine(dbDir, "expirewise.db");
                var connectionString = $"Filename={dbPath};Connection=shared";

                services.AddSingleton(new LiteDbContext(connectionString));
                services.AddSingleton<IUnitOfWork, LiteDbUnitOfWork>();

                // Services
                services.AddSingleton<DialogService>();
                services.AddSingleton<IFileImportExportService, FileImportExportService>();

                // Repository (only ExpireWise)
                services.AddSingleton<IExpireWiseRepository, ExpireWiseRepository>();

                // Parsers
                services.AddTransient<ExpireWiseParser>();

                // ViewModels
                services.AddTransient<ExpireWiseViewModel>();
            })
            .Build();
    }
}
