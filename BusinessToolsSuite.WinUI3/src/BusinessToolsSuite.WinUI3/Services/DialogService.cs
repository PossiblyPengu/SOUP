using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace BusinessToolsSuite.WinUI3.Services;

/// <summary>
/// Service for showing dialogs and file pickers in WinUI 3
/// </summary>
public class DialogService
{
    private Window? GetMainWindow()
    {
        return (Application.Current as App)?.MainWindow;
    }

    private IntPtr GetWindowHandle()
    {
        var window = GetMainWindow();
        if (window == null)
            throw new InvalidOperationException("Main window not found");

        return WindowNative.GetWindowHandle(window);
    }

    /// <summary>
    /// Show a content dialog
    /// </summary>
    public async Task<ContentDialogResult> ShowContentDialogAsync(
        string title,
        string content,
        string primaryButtonText = "OK",
        string? secondaryButtonText = null,
        string? closeButtonText = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            XamlRoot = GetMainWindow()?.Content?.XamlRoot
        };

        if (!string.IsNullOrEmpty(secondaryButtonText))
            dialog.SecondaryButtonText = secondaryButtonText;

        if (!string.IsNullOrEmpty(closeButtonText))
            dialog.CloseButtonText = closeButtonText;

        return await dialog.ShowAsync();
    }

    /// <summary>
    /// Show a custom content dialog with a control as content
    /// </summary>
    public async Task<ContentDialogResult> ShowCustomDialogAsync(
        string title,
        UIElement content,
        string primaryButtonText = "OK",
        string? secondaryButtonText = null,
        string? closeButtonText = null)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButtonText,
            XamlRoot = GetMainWindow()?.Content?.XamlRoot
        };

        if (!string.IsNullOrEmpty(secondaryButtonText))
            dialog.SecondaryButtonText = secondaryButtonText;

        if (!string.IsNullOrEmpty(closeButtonText))
            dialog.CloseButtonText = closeButtonText;

        return await dialog.ShowAsync();
    }

    /// <summary>
    /// Show an open file dialog
    /// </summary>
    public async Task<string?> ShowOpenFileDialogAsync(string title, params string[] fileTypeFilters)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        // Add filters
        if (fileTypeFilters.Length > 0)
        {
            foreach (var filter in fileTypeFilters)
            {
                picker.FileTypeFilter.Add(filter);
            }
        }
        else
        {
            picker.FileTypeFilter.Add("*");
        }

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// Show a save file dialog
    /// </summary>
    public async Task<string?> ShowSaveFileDialogAsync(
        string title,
        string defaultFileName,
        string defaultExtension,
        params (string Name, string Extension)[] fileTypeChoices)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = defaultFileName
        };

        InitializeWithWindow.Initialize(picker, GetWindowHandle());

        // Add file type choices
        if (fileTypeChoices.Length > 0)
        {
            foreach (var (name, extension) in fileTypeChoices)
            {
                picker.FileTypeChoices.Add(name, new[] { extension });
            }
        }
        else
        {
            picker.FileTypeChoices.Add("All Files", new[] { "*" });
        }

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>
    /// Show an error message dialog
    /// </summary>
    public async Task ShowErrorAsync(string title, string message)
    {
        await ShowContentDialogAsync(title, message, "OK");
    }

    /// <summary>
    /// Show a confirmation dialog
    /// </summary>
    public async Task<bool> ShowConfirmationAsync(string title, string message)
    {
        var result = await ShowContentDialogAsync(
            title,
            message,
            "Yes",
            "No");

        return result == ContentDialogResult.Primary;
    }
}
