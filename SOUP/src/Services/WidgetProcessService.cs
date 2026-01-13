using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SOUP.Services;

/// <summary>
/// Manages the widget as a separate process for complete isolation.
/// This allows the widget to run independently and survive modal dialogs,
/// and enables clean shutdown for updates.
/// </summary>
public sealed class WidgetProcessService : IDisposable
{
    private readonly ILogger<WidgetProcessService>? _logger;
    private Process? _widgetProcess;
    private readonly Lock _lock = new();
    private bool _disposed;

    public event Action? WidgetClosed;

    public WidgetProcessService(ILogger<WidgetProcessService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the widget process is currently running.
    /// </summary>
    public bool IsWidgetOpen
    {
        get
        {
            lock (_lock)
            {
                return _widgetProcess != null && !_widgetProcess.HasExited;
            }
        }
    }

    /// <summary>
    /// Launches the widget as a separate process.
    /// </summary>
    public void ShowWidget()
    {
        lock (_lock)
        {
            // If already running, bring to front
            if (_widgetProcess != null && !_widgetProcess.HasExited)
            {
                try
                {
                    BringProcessToFront(_widgetProcess);
                    _logger?.LogDebug("Widget process already running, brought to front");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to bring widget to front");
                }
                return;
            }

            try
            {
                // Get path to current executable
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath))
                {
                    _logger?.LogError("Could not determine executable path");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--widget",
                    UseShellExecute = false,
                    CreateNoWindow = false
                };

                _widgetProcess = Process.Start(startInfo);
                if (_widgetProcess != null)
                {
                    _widgetProcess.EnableRaisingEvents = true;
                    _widgetProcess.Exited += OnWidgetProcessExited;
                    _logger?.LogInformation("Widget process started (PID: {ProcessId})", _widgetProcess.Id);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to start widget process");
            }
        }
    }

    private void OnWidgetProcessExited(object? sender, EventArgs e)
    {
        lock (_lock)
        {
            var exitCode = _widgetProcess?.ExitCode ?? -1;
            _logger?.LogInformation("Widget process exited (Code: {ExitCode})", exitCode);
            _widgetProcess?.Dispose();
            _widgetProcess = null;
        }

        WidgetClosed?.Invoke();
    }

    /// <summary>
    /// Closes the widget process gracefully.
    /// </summary>
    public async Task CloseWidgetAsync(CancellationToken ct = default)
    {
        Process? process;
        lock (_lock)
        {
            process = _widgetProcess;
            if (process == null || process.HasExited)
            {
                _widgetProcess = null;
                return;
            }
        }

        try
        {
            _logger?.LogInformation("Requesting widget process to close");

            // Send close message to main window
            process.CloseMainWindow();

            // Wait for graceful exit
            var exited = await Task.Run(() => process.WaitForExit(3000), ct);

            if (!exited)
            {
                _logger?.LogWarning("Widget process did not exit gracefully, killing");
                process.Kill(entireProcessTree: true);
                await Task.Run(() => process.WaitForExit(1000), ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error closing widget process");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { /* Ignore */ }
        }
        finally
        {
            lock (_lock)
            {
                _widgetProcess?.Dispose();
                _widgetProcess = null;
            }
        }
    }

    /// <summary>
    /// Forcefully terminates the widget process.
    /// </summary>
    public void KillWidget()
    {
        lock (_lock)
        {
            if (_widgetProcess == null || _widgetProcess.HasExited)
            {
                _widgetProcess = null;
                return;
            }

            try
            {
                _logger?.LogInformation("Killing widget process");
                _widgetProcess.Kill(entireProcessTree: true);
                _widgetProcess.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error killing widget process");
            }
            finally
            {
                _widgetProcess?.Dispose();
                _widgetProcess = null;
            }
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static void BringProcessToFront(Process process)
    {
        var handle = process.MainWindowHandle;
        if (handle != IntPtr.Zero)
        {
            ShowWindow(handle, SW_RESTORE);
            SetForegroundWindow(handle);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Don't kill widget on dispose - let it run independently
        // The widget will shut down when user closes it
        lock (_lock)
        {
            if (_widgetProcess != null)
            {
                _widgetProcess.Exited -= OnWidgetProcessExited;
                _widgetProcess.Dispose();
                _widgetProcess = null;
            }
        }
    }
}
