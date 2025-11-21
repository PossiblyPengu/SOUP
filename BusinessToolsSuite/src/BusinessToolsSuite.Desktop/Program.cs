using System;
using BusinessToolsSuite.Desktop.Services;
using System.IO;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using BusinessToolsSuite.Infrastructure.Data;
using BusinessToolsSuite.Infrastructure.Repositories;
using BusinessToolsSuite.Infrastructure.Services;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Desktop.Services;
using BusinessToolsSuite.Shared.Services;
using BusinessToolsSuite.Desktop.ViewModels;

namespace BusinessToolsSuite.Desktop;

sealed class Program
{
    public static IHost? AppHost { get; private set; }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure Serilog
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appDataPath, "BusinessToolsSuite", "Logs");
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
            Log.Information("Starting Business Tools Suite");

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

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // Host configuration for dependency injection
    private static IHost BuildHost()
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Database
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dbDir = Path.Combine(appDataPath, "BusinessToolsSuite");
                Directory.CreateDirectory(dbDir);
                var dbPath = Path.Combine(dbDir, "businesstools.db");
                var connectionString = $"Filename={dbPath};Connection=shared";

                services.AddSingleton(new LiteDbContext(connectionString));
                services.AddSingleton<IUnitOfWork, LiteDbUnitOfWork>();

                // Repositories
                services.AddSingleton<IExpireWiseRepository, ExpireWiseRepository>();
                services.AddSingleton<IAllocationBuddyRepository, AllocationBuddyRepository>();
                services.AddSingleton<IEssentialsBuddyRepository, EssentialsBuddyRepository>();
                services.AddSingleton<IFileImportExportService, FileImportExportService>();

                // Services
                services.AddSingleton<NavigationService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<DialogService>();

                // ViewModels
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<LauncherViewModel>();
                services.AddTransient<Features.ExpireWise.ViewModels.ExpireWiseViewModel>();
                services.AddTransient<Features.AllocationBuddy.ViewModels.AllocationBuddyViewModel>();
                services.AddTransient<Features.EssentialsBuddy.ViewModels.EssentialsBuddyViewModel>();
            })
            .Build();
    }
}
