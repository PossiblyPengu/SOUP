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

namespace ExpireWise.Standalone;

/// <summary>
/// ExpireWise standalone application
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // Configure Serilog
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logDir = Path.Combine(appDataPath, "SOUP", "Logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "expirewise-.log");

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
            Log.Fatal(ex, "Failed to initialize ExpireWise");
            throw;
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Database configuration
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbDir = Path.Combine(appDataPath, "SOUP", "Data");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "SAP.db");

        // Infrastructure services
        services.AddSingleton(sp => new LiteDbContext(dbPath));
        services.AddScoped<IUnitOfWork, LiteDbUnitOfWork>();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(LiteDbRepository<>));
        services.AddScoped<IExpireWiseRepository, ExpireWiseRepository>();

        // Application services
        services.AddSingleton<IFileImportExportService, FileImportExportService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<SettingsService>();

        // ViewModels (Singleton persists data within standalone app)
        services.AddSingleton<ExpireWiseViewModel>();
        services.AddTransient<ExpirationItemDialogViewModel>();
        services.AddTransient<ExpireWiseSettingsViewModel>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Create and show ExpireWise window
        var viewModel = _host.Services.GetRequiredService<ExpireWiseViewModel>();
        var window = new ExpireWiseWindow(viewModel);
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
