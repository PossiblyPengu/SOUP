using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace SOUP.Services;

/// <summary>
/// Service for displaying dialogs, message boxes, and file pickers in WPF.
/// </summary>
/// <remarks>
/// <para>
/// This service provides a consistent API for all dialog interactions in the application,
/// including message boxes, confirmation dialogs, and file open/save dialogs.
/// </para>
/// <para>
/// All methods are async-compatible for consistency with MVVM patterns, though WPF
/// dialogs are inherently synchronous.
/// </para>
/// </remarks>
public class DialogService
{
    // Windows DWM API for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    
    /// <summary>
    /// Apply dark mode to the window title bar
    /// </summary>
    private static void ApplyDarkTitleBar(Window window)
    {
        if (!ThemeService.Instance.IsDarkMode) return;
        
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        int useImmersiveDarkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
    }

    /// <summary>
    /// Shows an informational message dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The message content.</param>
    /// <returns>A completed task.</returns>
    public Task ShowMessageAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows an error message dialog with an error icon.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The error message content.</param>
    /// <returns>A completed task.</returns>
    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows a Yes/No confirmation dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The confirmation message.</param>
    /// <returns><c>true</c> if the user clicked Yes; otherwise, <c>false</c>.</returns>
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>
    /// Shows an open file dialog that allows selecting multiple files.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="filterName">The display name for the file type filter.</param>
    /// <param name="extensions">File extensions to filter (e.g., "xlsx", "csv").</param>
    /// <returns>Array of selected file paths, or <c>null</c> if cancelled.</returns>
    public Task<string[]?> ShowOpenFileDialogAsync(string title, string filterName, params string[] extensions)
    {
        var filterParts = new System.Collections.Generic.List<string>();
        if (extensions != null && extensions.Length > 0)
        {
            var extList = string.Join(";", extensions.Select(e => $"*.{e.TrimStart('*', '.')}"));
            filterParts.Add($"{filterName} ({extList})|{extList}");
        }
        filterParts.Add("All Files (*.*)|*.*");

        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = string.Join("|", filterParts),
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = true
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileNames : null);
    }

    /// <summary>
    /// Shows a save file dialog.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="defaultFileName">The default file name.</param>
    /// <param name="filter">The file type filter string (e.g., "CSV Files|*.csv").</param>
    /// <returns>The selected file path, or <c>null</c> if cancelled.</returns>
    public Task<string?> ShowSaveFileDialogAsync(string title, string defaultFileName, string filter = "All Files|*.*")
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = defaultFileName,
            Filter = filter,
            CheckPathExists = true
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    /// <summary>
    /// Shows a custom window as a modal dialog.
    /// </summary>
    /// <param name="dialog">The window to display.</param>
    /// <returns>The dialog result.</returns>
    public Task<bool?> ShowDialogAsync(Window dialog)
    {
        dialog.Owner = Application.Current.MainWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        return Task.FromResult(dialog.ShowDialog());
    }

    /// <summary>
    /// Shows a UserControl embedded in a dialog window and returns a result.
    /// </summary>
    /// <typeparam name="T">The type of result expected from the dialog.</typeparam>
    /// <param name="content">The UserControl to display as dialog content.</param>
    /// <returns>The result from the dialog, or default if closed without result.</returns>
    /// <remarks>
    /// The content UserControl should set its Tag property to an <see cref="Action{T}"/>
    /// and invoke it with the result when the dialog should close.
    /// </remarks>
    public Task<T> ShowContentDialogAsync<T>(System.Windows.Controls.UserControl content)
    {
        var tcs = new TaskCompletionSource<T>();

        // Get the background color from the current theme
        var backgroundColor = ThemeService.Instance.IsDarkMode
            ? System.Windows.Media.Color.FromRgb(30, 30, 46)   // Dark theme surface color
            : System.Windows.Media.Color.FromRgb(255, 255, 255); // Light theme

        var dialog = new Window
        {
            Content = content,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.SingleBorderWindow,
            ResizeMode = ResizeMode.CanResizeWithGrip,
            MinWidth = 400,
            MinHeight = 300,
            MaxWidth = 800,
            MaxHeight = 900,
            ShowInTaskbar = false,
            Background = new System.Windows.Media.SolidColorBrush(backgroundColor)
        };

        // Apply dark title bar when in dark mode
        dialog.SourceInitialized += (s, e) => ApplyDarkTitleBar(dialog);

        // Store the result handler
        content.Tag = new Action<T>(result =>
        {
            tcs.TrySetResult(result);
            dialog.Close();
        });

        dialog.ShowDialog();

        // If not already set, return default
        if (!tcs.Task.IsCompleted)
        {
            tcs.TrySetResult(default(T)!);
        }

        return tcs.Task;
    }
}
