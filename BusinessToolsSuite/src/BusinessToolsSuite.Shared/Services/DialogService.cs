using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;

namespace BusinessToolsSuite.Shared.Services;

/// <summary>
/// Service for showing dialogs and file pickers
/// </summary>
public class DialogService
{
    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    /// <summary>
    /// Show a dialog window
    /// </summary>
    public async Task<TResult?> ShowDialogAsync<TResult>(Window dialog)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
            return default;

        return await dialog.ShowDialog<TResult>(mainWindow);
    }

    /// <summary>
    /// Show an open file dialog
    /// </summary>
    public async Task<string[]?> ShowOpenFileDialogAsync(string title, params string[] filters)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
            return null;

        var dialog = new OpenFileDialog
        {
            Title = title,
            AllowMultiple = false
        };

        if (filters.Length > 0)
        {
            dialog.Filters = new System.Collections.Generic.List<FileDialogFilter>();
            for (int i = 0; i < filters.Length; i += 2)
            {
                if (i + 1 < filters.Length)
                {
                    dialog.Filters.Add(new FileDialogFilter
                    {
                        Name = filters[i],
                        Extensions = new System.Collections.Generic.List<string> { filters[i + 1] }
                    });
                }
            }
        }

        return await dialog.ShowAsync(mainWindow);
    }

    /// <summary>
    /// Show a save file dialog
    /// </summary>
    public async Task<string?> ShowSaveFileDialogAsync(string title, string defaultExtension, params string[] filters)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
            return null;

        var dialog = new SaveFileDialog
        {
            Title = title,
            DefaultExtension = defaultExtension
        };

        if (filters.Length > 0)
        {
            dialog.Filters = new System.Collections.Generic.List<FileDialogFilter>();
            for (int i = 0; i < filters.Length; i += 2)
            {
                if (i + 1 < filters.Length)
                {
                    dialog.Filters.Add(new FileDialogFilter
                    {
                        Name = filters[i],
                        Extensions = new System.Collections.Generic.List<string> { filters[i + 1] }
                    });
                }
            }
        }

        return await dialog.ShowAsync(mainWindow);
    }
}
