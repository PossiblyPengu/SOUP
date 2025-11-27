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

namespace BusinessToolsSuite.WPF;

/// <summary>
/// Interaction logic for App.xaml
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
            Log.Information("Starting Business Tools Suite - WPF");

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
            Log.Fatal(ex, "Failed to initialize application");
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
        services.AddScoped<IAllocationBuddyRepository, AllocationBuddyRepository>();
        services.AddScoped<IEssentialsBuddyRepository, EssentialsBuddyRepository>();
        services.AddScoped<IExpireWiseRepository, ExpireWiseRepository>();

        // Application services
        services.AddSingleton<IFileImportExportService, FileImportExportService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<BusinessToolsSuite.Infrastructure.Services.SettingsService>();
        services.AddSingleton<BusinessToolsSuite.Infrastructure.Services.Parsers.AllocationBuddyParser>(sp => new BusinessToolsSuite.Infrastructure.Services.Parsers.AllocationBuddyParser(null));

        // ViewModels - Shell
        services.AddSingleton<LauncherViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<UnifiedSettingsViewModel>();
        services.AddTransient<DictionaryManagementViewModel>();

        // ViewModels - AllocationBuddy (Singletons persist data across navigation)
        services.AddSingleton<AllocationBuddyRPGViewModel>();
        services.AddTransient<SelectLocationDialogViewModel>();
        services.AddTransient<AllocationBuddySettingsViewModel>();

        // ViewModels - EssentialsBuddy (Singleton persists data across navigation)
        services.AddSingleton<EssentialsBuddyViewModel>();
        services.AddTransient<InventoryItemDialogViewModel>();
        services.AddTransient<EssentialsBuddySettingsViewModel>();

        // ViewModels - ExpireWise (Singleton persists data across navigation)
        services.AddSingleton<ExpireWiseViewModel>();
        services.AddTransient<ExpirationItemDialogViewModel>();
        services.AddTransient<ExpireWiseSettingsViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

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

    public static T GetService<T>() where T : class
    {
        if (Current is not App app)
            throw new InvalidOperationException("App is not initialized");

        return app._host.Services.GetRequiredService<T>();
    }
}

