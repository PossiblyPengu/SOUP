using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SAP.Services;

/// <summary>
/// Service for showing dialogs and file pickers in WPF
/// </summary>
public class DialogService
{
    /// <summary>
    /// Show an information message dialog
    /// </summary>
    public Task ShowMessageAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Show an error message dialog
    /// </summary>
    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Show a confirmation dialog
    /// </summary>
    public Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes);
    }

    /// <summary>
    /// Show an open file dialog (supports multiple files)
    /// </summary>
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
    /// Show a save file dialog
    /// </summary>
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
    /// Show a custom window dialog
    /// </summary>
    public Task<bool?> ShowDialogAsync(Window dialog)
    {
        dialog.Owner = Application.Current.MainWindow;
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        return Task.FromResult(dialog.ShowDialog());
    }

    /// <summary>
    /// Show a custom content dialog (UserControl embedded in a Window)
    /// </summary>
    public Task<T> ShowContentDialogAsync<T>(System.Windows.Controls.UserControl content)
    {
        var tcs = new TaskCompletionSource<T>();

        var dialog = new Window
        {
            Content = content,
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStyle = WindowStyle.ToolWindow,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false
        };

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
