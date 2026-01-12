using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Windows;
using Serilog;
using Serilog.Events;
using SOUP.Core.Interfaces;
using SOUP.Data;
using SOUP.Features.OrderLog.ViewModels;
using SOUP.Infrastructure.Data;
using SOUP.Infrastructure.Repositories;
using SOUP.Infrastructure.Services;
using SOUP.Services;
using SOUP.ViewModels;
using SOUP.Windows;

namespace SOUP;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    /// <summary>
    /// Gets or sets whether the application is currently applying an update.
    /// When true, all closing confirmations and events are bypassed.
    /// </summary>
    public static bool IsUpdating { get; set; }

    public App()
    {
        // Configure Serilog
        Directory.CreateDirectory(Core.AppPaths.LogsDir);
        var logPath = Path.Combine(Core.AppPaths.LogsDir, "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();

        try
        {
            Log.Information("Starting S.O.U.P (S.A.M. Operations Utilities Pack)");

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
                    try
                    {
                        var tmp = Path.Combine(Path.GetTempPath(), "SOUP_RuntimeException.log");
                        File.AppendAllText(tmp, DateTime.Now.ToString("o") + "\n" + ev.Exception.ToString() + "\n\n");
                    }
                    catch { }
                    try
                    {
                        var repoLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "SOUP_RuntimeException.log");
                        File.AppendAllText(repoLog, DateTime.Now.ToString("o") + "\n" + ev.Exception.ToString() + "\n\n");
                    }
                    catch { }
                    // Prevent modal exception dialogs during automated runs; mark handled so app can continue to log.
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
        // Ensure all app directories exist
        Core.AppPaths.EnsureDirectoriesExist();

        // Infrastructure services - SQLite database
        services.AddSingleton(sp => new SqliteDbContext(
            Core.AppPaths.MainDbPath,
            sp.GetService<ILogger<SqliteDbContext>>()));
        services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();

        // Shared dictionary database (items and stores for matching across modules)
        services.AddSingleton(_ => DictionaryDbContext.Instance);

        // Repositories - Singletons to match ViewModel lifetimes (prevents captive dependency)
        // Only register repositories for enabled modules
        services.AddSingleton(typeof(IRepository<>), typeof(SqliteRepository<>));
        
        var moduleConfig = ModuleConfiguration.Instance;
        
        if (moduleConfig.AllocationBuddyEnabled)
            services.AddSingleton<IAllocationBuddyRepository, AllocationBuddyRepository>();
        if (moduleConfig.EssentialsBuddyEnabled)
            services.AddSingleton<IEssentialsBuddyRepository, EssentialsBuddyRepository>();
        if (moduleConfig.ExpireWiseEnabled)
            services.AddSingleton<IExpireWiseRepository, ExpireWiseRepository>();

        // Application services
        services.AddSingleton<IFileImportExportService, FileImportExportService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<SOUP.Infrastructure.Services.SettingsService>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<UpdateService>();
        services.AddSingleton<SOUP.Infrastructure.Services.Parsers.AllocationBuddyParser>(sp => 
            new SOUP.Infrastructure.Services.Parsers.AllocationBuddyParser(
                sp.GetService<ILogger<SOUP.Infrastructure.Services.Parsers.AllocationBuddyParser>>()));

        // ViewModels - Shell
        services.AddSingleton<LauncherViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<UnifiedSettingsViewModel>();
        services.AddSingleton<ApplicationSettingsViewModel>();
        services.AddTransient<DictionaryManagementViewModel>(sp => 
            new DictionaryManagementViewModel(
                sp.GetService<SOUP.Services.External.DictionarySyncService>(),
                sp.GetService<ILogger<DictionaryManagementViewModel>>()));

        // ViewModels - AllocationBuddy (Singletons persist data across navigation)
        if (moduleConfig.AllocationBuddyEnabled)
        {
            services.AddSingleton<AllocationBuddyRPGViewModel>();
            services.AddTransient<SelectLocationDialogViewModel>();
            services.AddTransient<AllocationBuddySettingsViewModel>();
        }

        // ViewModels - EssentialsBuddy (Singleton persists data across navigation)
        if (moduleConfig.EssentialsBuddyEnabled)
        {
            services.AddSingleton<EssentialsBuddyViewModel>();
            services.AddTransient<InventoryItemDialogViewModel>();
            services.AddTransient<EssentialsBuddySettingsViewModel>();
        }

        // ViewModels - ExpireWise (Singleton persists data across navigation)
        if (moduleConfig.ExpireWiseEnabled)
        {
            services.AddSingleton<ExpireWiseViewModel>();
            services.AddTransient<ExpirationItemDialogViewModel>();
            services.AddTransient<ExpireWiseSettingsViewModel>();
        }

        // ViewModels - OrderLog (persist with SQLite, using singleton factory)
        if (moduleConfig.OrderLogEnabled)
        {
            services.AddSingleton<SOUP.Features.OrderLog.Services.IOrderLogService>(sp =>
                SOUP.Features.OrderLog.Services.OrderLogRepository.GetInstance(
                    sp.GetService<ILogger<SOUP.Features.OrderLog.Services.OrderLogRepository>>()));
            services.AddSingleton<SOUP.Features.OrderLog.ViewModels.OrderLogViewModel>();
            services.AddSingleton<SOUP.Features.OrderLog.Services.GroupStateStore>();
        }

        // ViewModels - SwiftLabel
        if (moduleConfig.SwiftLabelEnabled)
        {
            services.AddTransient<SwiftLabelViewModel>();
        }

        // External Data Services (MySQL and Business Central)
        services.AddHttpClient("BusinessCentral", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
        });
        
        services.AddSingleton<SOUP.Services.External.MySqlDataService>();
        services.AddSingleton<SOUP.Services.External.BusinessCentralService>(sp =>
            new SOUP.Services.External.BusinessCentralService(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetService<ILogger<SOUP.Services.External.BusinessCentralService>>()));
        services.AddSingleton<SOUP.Services.External.DictionarySyncService>();
        services.AddTransient<ExternalDataViewModel>();

        // Services - Navigation
        services.AddSingleton<NavOrderService>();
        
        // Services - App Lifecycle
        if (moduleConfig.OrderLogEnabled)
        {
            services.AddSingleton<WidgetProcessService>();
        }
        services.AddSingleton<AppLifecycleService>();

        // Windows
        services.AddSingleton<MainWindow>();
        if (moduleConfig.OrderLogEnabled)
        {
            services.AddTransient<Windows.OrderLogWidgetWindow>();
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Show splash screen immediately for main app only (not for widget process)
            Windows.SplashWindow? splash = null;
            if (!AppLifecycleService.IsWidgetProcess)
            {
                splash = new Windows.SplashWindow();
                splash.Show();
                splash.SetStatus("Initializing services...");
            }
            
            await _host.StartAsync();

            // Initialize theme service (must be on UI thread)
            Dispatcher.Invoke(() =>
            {
                var themeService = _host.Services.GetRequiredService<ThemeService>();
                themeService.Initialize();
                splash?.SetStatus("Applying theme...");
                Log.Information("Theme initialized: {Theme}", themeService.IsDarkMode ? "Dark" : "Light");
            });

            // Initialize tray icon service (must be on UI thread)
            Dispatcher.Invoke(() =>
            {
                var trayService = _host.Services.GetRequiredService<TrayIconService>();
                trayService.Initialize();
                splash?.SetStatus("Initializing UI...");
                Log.Information("Tray icon initialized");
            });

            // Initialize the shared dictionary database at startup
            try
            {
                var dictDb = DictionaryDbContext.Instance;
                var itemCount = dictDb.GetItemCount();
                var storeCount = dictDb.GetStoreCount();
                splash?.SetStatus("Loading dictionaries...");
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

            // Load persisted data for enabled modules only
            var moduleConfig = ModuleConfiguration.Instance;
            try
            {
                if (moduleConfig.EssentialsBuddyEnabled)
                {
                    var essentialsViewModel = _host.Services.GetService<EssentialsBuddyViewModel>();
                    if (essentialsViewModel != null)
                    {
                        splash?.SetStatus("Loading EssentialsBuddy...");
                        await essentialsViewModel.LoadPersistedDataAsync();
                        Log.Information("EssentialsBuddy data loaded");
                    }
                }

                if (moduleConfig.ExpireWiseEnabled)
                {
                    var expireWiseViewModel = _host.Services.GetService<ExpireWiseViewModel>();
                    if (expireWiseViewModel != null)
                    {
                        splash?.SetStatus("Loading ExpireWise...");
                        await expireWiseViewModel.LoadPersistedDataAsync();
                        Log.Information("ExpireWise data loaded");
                    }
                }

                // AllocationBuddy auto-loads from archives via LoadArchivesAsync
                if (moduleConfig.AllocationBuddyEnabled)
                {
                    var allocationViewModel = _host.Services.GetService<AllocationBuddyRPGViewModel>();
                    if (allocationViewModel != null)
                    {
                        splash?.SetStatus("Loading AllocationBuddy...");
                        await allocationViewModel.LoadMostRecentArchiveAsync();
                        Log.Information("AllocationBuddy data loaded");
                    }
                }

                Log.Information("Persisted data loaded for enabled modules");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error loading persisted app data");
            }

            // Load application settings for startup behavior
            var settingsService = _host.Services.GetRequiredService<Infrastructure.Services.SettingsService>();
            var appSettings = await settingsService.LoadSettingsAsync<Core.Entities.Settings.ApplicationSettings>("Application");
            var lifecycleService = _host.Services.GetRequiredService<AppLifecycleService>();

            splash?.SetStatus("Starting application...");

            // Check command-line arguments
            if (AppLifecycleService.IsWidgetProcess)
            {
                // Running as separate widget process (--widget flag)
                if (moduleConfig.OrderLogEnabled)
                {
                    var viewModel = _host.Services.GetRequiredService<OrderLogViewModel>();
                    var widgetWindow = new OrderLogWidgetWindow(viewModel, _host.Services);
                    
                    widgetWindow.Closed += (s, args) =>
                    {
                        Log.Information("Widget closed, shutting down");
                        Shutdown();
                    };
                    
                    widgetWindow.Show();
                    Log.Information("Running in widget-only mode (--widget flag)");
                }
                else
                {
                    Log.Warning("OrderLog widget requested but module is disabled");
                    var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                }
            }
            else
            {
                // Normal startup (no --widget flag)
                var widgetOnlyMode = appSettings.WidgetOnlyMode && moduleConfig.OrderLogEnabled;
                var launchWidget = !AppLifecycleService.HasNoWidgetFlag && 
                                   appSettings.LaunchWidgetOnStartup && 
                                   moduleConfig.OrderLogEnabled;

                if (widgetOnlyMode)
                {
                    // Widget-only mode
                    var widgetProcessService = _host.Services.GetService<WidgetProcessService>();
                    if (widgetProcessService != null)
                    {
                        widgetProcessService.WidgetClosed += () =>
                        {
                            Log.Information("Widget closed in widget-only mode, shutting down");
                            Dispatcher.Invoke(() => Shutdown());
                        };
                        widgetProcessService.ShowWidget();
                    }
                    Log.Information("Running in widget-only mode (settings)");
                }
                else
                {
                    // Normal mode - show main window
                    var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                    
                    // Launch widget if configured
                    if (launchWidget)
                    {
                        var widgetProcessService = _host.Services.GetService<WidgetProcessService>();
                        widgetProcessService?.ShowWidget();
                        Log.Information("Widget launched on startup");
                    }
                }
            }
            
            // Close splash screen with animation (only if shown)
            if (splash != null)
            {
                await splash.CloseAsync();
            }

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
            // Save all app data before closing
            try
            {
                // Archive AllocationBuddy data
                var allocationViewModel = _host.Services.GetService<AllocationBuddyRPGViewModel>();
                if (allocationViewModel != null)
                {
                    await allocationViewModel.ArchiveOnShutdownAsync();
                    Log.Information("AllocationBuddy data archived on shutdown");
                }

                // Save EssentialsBuddy data
                var essentialsViewModel = _host.Services.GetService<EssentialsBuddyViewModel>();
                if (essentialsViewModel != null)
                {
                    await essentialsViewModel.SaveDataOnShutdownAsync();
                    Log.Information("EssentialsBuddy data saved on shutdown");
                }

                // Save ExpireWise data
                var expireWiseViewModel = _host.Services.GetService<ExpireWiseViewModel>();
                if (expireWiseViewModel != null)
                {
                    await expireWiseViewModel.SaveDataOnShutdownAsync();
                    Log.Information("ExpireWise data saved on shutdown");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error saving app data on shutdown");
            }

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

