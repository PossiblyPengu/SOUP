using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;
using SAP.Core.Interfaces;
using SAP.Data;
using SAP.Infrastructure.Data;
using SAP.Infrastructure.Repositories;
using SAP.Infrastructure.Services;
using SAP.Services;
using SAP.ViewModels;

namespace SAP;

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
        var logDir = Path.Combine(appDataPath, "SAP", "Logs");
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
            Log.Information("Starting S.A.P (S.A.M. Add-on Pack)");

            // Global exception handlers to catch and report unhandled errors during startup/runtime
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try
                {
                    var ex = ev.ExceptionObject as Exception;
                    Log.Fatal(ex, "Unhandled exception in AppDomain");
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log AppDomain exception: {logEx.Message}");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                try
                {
                    Log.Fatal(ev.Exception, "Unobserved task exception");
                    ev.SetObserved();
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to log task exception: {logEx.Message}");
                }
            };

            this.DispatcherUnhandledException += (s, ev) =>
            {
                try
                {
                    Log.Fatal(ev.Exception, "Dispatcher unhandled exception");
                    MessageBox.Show($"An unexpected error occurred:\n{ev.Exception.Message}", "Unhandled Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    ev.Handled = true;
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to handle dispatcher exception: {logEx.Message}");
                }
            };

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
        var dbDir = Path.Combine(appDataPath, "SAP", "Data");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "SAP.db");

        // Infrastructure services
        services.AddSingleton(sp => new LiteDbContext(dbPath));
        services.AddScoped<IUnitOfWork, LiteDbUnitOfWork>();

        // Shared dictionary database (items and stores for matching across modules)
        services.AddSingleton(_ => DictionaryDbContext.Instance);

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
        services.AddSingleton<SAP.Infrastructure.Services.SettingsService>();
        services.AddSingleton<SAP.Infrastructure.Services.Parsers.AllocationBuddyParser>(sp => new SAP.Infrastructure.Services.Parsers.AllocationBuddyParser(null));

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

        // ViewModels - SwiftLabel
        services.AddTransient<SwiftLabelViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            await _host.StartAsync().ConfigureAwait(false);

            // Initialize theme service
            var themeService = _host.Services.GetRequiredService<ThemeService>();
            themeService.Initialize();
            Log.Information("Theme initialized: {Theme}", themeService.IsDarkMode ? "Dark" : "Light");

            // Initialize the shared dictionary database at startup
            // This loads items and stores that are used across multiple modules
            try
            {
                var dictDb = DictionaryDbContext.Instance;
                var itemCount = dictDb.Items.Count();
                var storeCount = dictDb.Stores.Count();
                Log.Information("Dictionary database initialized: {ItemCount} items, {StoreCount} stores", itemCount, storeCount);
                
                if (itemCount == 0)
                {
                    Log.Warning("Dictionary database is empty. Run the ImportDictionary tool to populate it.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize dictionary database");
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed during application startup");
            MessageBox.Show($"Failed to start application: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            // Dispose dictionary database
            try
            {
                DictionaryDbContext.Instance.Dispose();
                Log.Information("Dictionary database closed");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error closing dictionary database");
            }

            using (_host)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application shutdown");
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    public static T GetService<T>() where T : class
    {
        if (Current is not App app)
            throw new InvalidOperationException("App is not initialized");

        return app._host.Services.GetRequiredService<T>();
    }
}

