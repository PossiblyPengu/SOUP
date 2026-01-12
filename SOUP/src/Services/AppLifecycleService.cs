using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SOUP.Services;

/// <summary>
/// Centralizes all application lifecycle management including:
/// - Process detection and management (main app vs widget)
/// - Window opening/closing behavior
/// - Close-to-tray logic
/// - Update process handling
/// </summary>
public sealed class AppLifecycleService
{
    private readonly ILogger<AppLifecycleService>? _logger;
    private readonly WidgetProcessService? _widgetProcessService;

    public AppLifecycleService(
        ILogger<AppLifecycleService>? logger = null,
        WidgetProcessService? widgetProcessService = null)
    {
        _logger = logger;
        _widgetProcessService = widgetProcessService;
    }

    /// <summary>
    /// Checks if the application is running as a separate widget process (launched with --widget flag).
    /// </summary>
    public static bool IsWidgetProcess => Environment.GetCommandLineArgs().Any(arg =>
        arg.Equals("--widget", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks if the application was launched with --no-widget flag.
    /// This is used when opening main app from the widget to prevent duplicate widget launch.
    /// </summary>
    public static bool HasNoWidgetFlag => Environment.GetCommandLineArgs().Any(arg =>
        arg.Equals("--no-widget", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets whether the widget process is currently running.
    /// </summary>
    public bool IsWidgetRunning => _widgetProcessService?.IsWidgetOpen == true;

    /// <summary>
    /// Launches the main application window.
    /// If main app is already running, activates it instead.
    /// </summary>
    public void OpenMainWindow()
    {
        try
        {
            if (IsWidgetProcess)
            {
                // Running as separate widget process - launch/activate main app
                LaunchOrActivateMainApp();
            }
            else
            {
                // Running within main app - activate main window if it exists
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var mainWindow = Application.Current?.Windows
                        .OfType<MainWindow>()
                        .FirstOrDefault();

                    if (mainWindow != null)
                    {
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open main window");
        }
    }

    /// <summary>
    /// Handles main window closing logic.
    /// Determines whether to close, hide to tray, or keep running based on settings.
    /// </summary>
    public bool HandleMainWindowClosing(
        bool closeToTray,
        bool confirmBeforeExit)
    {
        // If updating, bypass all logic and allow close
        if (App.IsUpdating)
        {
            return true;
        }

        var widgetOpen = IsWidgetRunning;
        var keepWidgetRunning = true; // TODO: Get from settings

        // If close-to-tray or widget is running and should be kept alive, hide instead
        if (closeToTray || (widgetOpen && keepWidgetRunning))
        {
            return false; // Don't close, will be hidden instead
        }

        // Check if user should confirm exit
        if (confirmBeforeExit)
        {
            // Return false to let caller show confirmation dialog
            return false;
        }

        // Allow close
        return true;
    }

    /// <summary>
    /// Handles cleanup when main window is closing.
    /// </summary>
    public void OnMainWindowClosing(bool keepWidgetRunning)
    {
        if (App.IsUpdating)
        {
            return;
        }

        // Close widget if it's running and shouldn't be kept alive
        if (IsWidgetRunning && !keepWidgetRunning)
        {
            _widgetProcessService?.KillWidget();
        }

        // Cleanup can be added here
    }

    /// <summary>
    /// Performs graceful shutdown of the application.
    /// Closes widget if running, then exits.
    /// </summary>
    public void GracefulShutdown()
    {
        try
        {
            _logger?.LogInformation("Initiating graceful shutdown");

            // Close widget if it's running
            if (IsWidgetRunning)
            {
                _widgetProcessService?.KillWidget();
            }

            // Give processes time to clean up
            System.Threading.Thread.Sleep(500);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    Application.Current?.Shutdown();
                }
                catch { }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during graceful shutdown");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Force closes all SOUP processes (for update process).
    /// </summary>
    public async System.Threading.Tasks.Task ForceCloseAllProcessesAsync()
    {
        try
        {
            _logger?.LogInformation("Force closing all SOUP processes");

            var currentProcess = Process.GetCurrentProcess();
            var allSoupProcesses = Process.GetProcessesByName("SOUP");

            foreach (var process in allSoupProcesses)
            {
                if (process.Id != currentProcess.Id)
                {
                    try
                    {
                        _logger?.LogInformation("Killing SOUP process {ProcessId}", process.Id);
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to kill process {ProcessId}", process.Id);
                    }
                }
                process.Dispose();
            }

            await System.Threading.Tasks.Task.Delay(500);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error force closing processes");
        }
    }

    /// <summary>
    /// Launches the main app or activates it if already running.
    /// Used when opening from widget process.
    /// </summary>
    private void LaunchOrActivateMainApp()
    {
        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
        {
            _logger?.LogError("Could not determine SOUP executable path");
            return;
        }

        var currentPid = Environment.ProcessId;
        var soupProcesses = Process.GetProcessesByName("SOUP")
            .Where(p => p.Id != currentPid)
            .ToList();

        bool mainAppFound = false;

        // Try to find and activate existing main window
        if (soupProcesses.Count > 0)
        {
            foreach (var mainProcess in soupProcesses)
            {
                try
                {
                    var handle = mainProcess.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindow(handle, SW_RESTORE);
                        SetForegroundWindow(handle);
                        mainAppFound = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to activate process {ProcessId}", mainProcess.Id);
                }
                finally
                {
                    mainProcess.Dispose();
                }
            }

            // Clean up remaining handles
            foreach (var p in soupProcesses)
            {
                p.Dispose();
            }
        }

        // If no main window found, launch new instance
        if (!mainAppFound)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--no-widget", // Prevent duplicate widget launch
                    UseShellExecute = false
                });
                _logger?.LogInformation("Launched new main app instance");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to launch main app");
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;
}
