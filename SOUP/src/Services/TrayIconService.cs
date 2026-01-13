using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using SOUP.Core.Entities.Settings;
using SOUP.Infrastructure.Services;
using Application = System.Windows.Application;

namespace SOUP.Services;

/// <summary>
/// Service for managing the system tray icon
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly SettingsService _settingsService;
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    /// <summary>
    /// Event raised when user requests to show the main window from tray
    /// </summary>
    public event Action? ShowRequested;

    /// <summary>
    /// Event raised when user requests to exit the application from tray
    /// </summary>
    public event Action? ExitRequested;

    /// <summary>
    /// Whether close to tray is enabled
    /// </summary>
    public bool CloseToTray { get; private set; }

    /// <summary>
    /// Whether the tray icon should be visible
    /// </summary>
    public bool ShowTrayIcon { get; private set; } = true;

    /// <summary>
    /// Whether to start minimized to tray
    /// </summary>
    public bool StartMinimizedToTray { get; private set; }

    /// <summary>
    /// Whether to confirm before exiting
    /// </summary>
    public bool ConfirmBeforeExit { get; private set; }

    /// <summary>
    /// Whether to keep the widget running when main window closes
    /// </summary>
    public bool KeepWidgetRunning { get; private set; } = true;

    public TrayIconService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;

        // Load initial settings synchronously
        LoadSettings();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.AppName == "Application")
        {
            LoadSettings();
            UpdateTrayIconVisibility();
        }
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsService.LoadSettingsAsync<ApplicationSettings>("Application")
                .GetAwaiter().GetResult();

            CloseToTray = settings.CloseToTray;
            ShowTrayIcon = settings.ShowTrayIcon;
            StartMinimizedToTray = settings.StartMinimizedToTray;
            ConfirmBeforeExit = settings.ConfirmBeforeExit;
            KeepWidgetRunning = settings.KeepWidgetRunning;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load tray settings");
        }
    }

    /// <summary>
    /// Initialize the tray icon. Call this from the UI thread after app starts.
    /// </summary>
    public void Initialize()
    {
        if (_notifyIcon != null) return;

        _notifyIcon = new()
        {
            Text = "S.O.U.P - S.A.M. Operations Utilities Pack",
            Visible = ShowTrayIcon
        };

        // Load icon from application
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "soup.ico");
            if (File.Exists(iconPath))
            {
                _notifyIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Try to get from executing assembly
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "SOUP.soup.ico";
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    _notifyIcon.Icon = new Icon(stream);
                }
                else
                {
                    // Use default system icon as fallback
                    _notifyIcon.Icon = SystemIcons.Application;
                }
            }
        }
        catch
        {
            _notifyIcon.Icon = SystemIcons.Application;
        }

        // Create context menu
        var contextMenu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show S.O.U.P");
        showItem.Click += (s, e) => ShowRequested?.Invoke();
        showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitRequested?.Invoke();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        // Double-click to show
        _notifyIcon.DoubleClick += (s, e) => ShowRequested?.Invoke();
    }

    /// <summary>
    /// Update tray icon visibility based on settings
    /// </summary>
    public void UpdateTrayIconVisibility()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = ShowTrayIcon;
        }
    }

    /// <summary>
    /// Show a balloon notification
    /// </summary>
    public void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, text, icon);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _settingsService.SettingsChanged -= OnSettingsChanged;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        GC.SuppressFinalize(this);
    }
}
