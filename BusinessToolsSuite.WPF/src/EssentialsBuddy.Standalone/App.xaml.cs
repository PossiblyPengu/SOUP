using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;
using BusinessToolsSuite.Core.Interfaces;
using BusinessToolsSuite.Infrastructure.Data;
using BusinessToolsSuite.Infrastructure.Repositories;
using BusinessToolsSuite.Infrastructure.Services;
using BusinessToolsSuite.WPF.Services;
using BusinessToolsSuite.WPF.ViewModels;
using BusinessToolsSuite.WPF.Windows;

namespace EssentialsBuddy.Standalone;

/// <summary>
/// Essentials Buddy standalone application
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // Configure Serilog
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appDataPath, "BusinessToolsSuite", "Logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "essentialsbuddy-.log");

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
            Log.Information("Starting Essentials Buddy");

            // Build host with dependency injection
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    ConfigureServices(services);
                })
                .Build();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to initialize Essentials Buddy");
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database configuration
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDir = Path.Combine(appDataPath, "BusinessToolsSuite", "Data");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "BusinessToolsSuite.db");

        // Infrastructure services
        services.AddSingleton(sp => new LiteDbContext(dbPath));
        services.AddScoped<IUnitOfWork, LiteDbUnitOfWork>();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(LiteDbRepository<>));
        services.AddScoped<IEssentialsBuddyRepository, EssentialsBuddyRepository>();

        // Application services
        services.AddSingleton<IFileImportExportService, FileImportExportService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<SettingsService>();

        // ViewModels (Singleton persists data within standalone app)
        services.AddSingleton<EssentialsBuddyViewModel>();
        services.AddTransient<InventoryItemDialogViewModel>();
        services.AddTransient<EssentialsBuddySettingsViewModel>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Create and show Essentials Buddy window
        var viewModel = _host.Services.GetRequiredService<EssentialsBuddyViewModel>();
        var window = new EssentialsBuddyWindow(viewModel);
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
