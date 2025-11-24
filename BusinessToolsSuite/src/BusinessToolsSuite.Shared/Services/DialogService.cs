using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Platform;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using BusinessToolsSuite.Shared.Controls;

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

        // If there is an in-app host available, prefer showing the dialog as an overlay
        if (InAppDialogHost.Instance != null)
        {
            // Try to extract a UserControl from the Window by using its content
            if (dialog.Content is Control c)
            {
                var obj = await InAppDialogHost.Instance.ShowDialogAsync<object?>(c);
                try
                {
                    var dc = c.DataContext;
                    if (dc != null)
                    {
                        var prop = dc.GetType().GetProperty("SelectedLocation") ?? dc.GetType().GetProperty("SelectedItem");
                        if (prop != null)
                        {
                            var val = prop.GetValue(dc);
                            return (TResult?)val;
                        }
                    }
                }
                catch { }
                return (TResult?)obj;
            }
        }

        // Fallback to showing a native window dialog
        var result = await dialog.ShowDialog<TResult>(mainWindow);
        try
        {
            // If DataContext has SelectedLocation or SelectedItem, return it
            var dc = dialog.DataContext;
            if (dc != null)
            {
                var prop = dc.GetType().GetProperty("SelectedLocation") ?? dc.GetType().GetProperty("SelectedItem");
                if (prop != null)
                {
                    var val = prop.GetValue(dc);
                    return (TResult?)val;
                }
            }
        }
        catch { }
        return (TResult?)result;
    }

    /// <summary>
    /// Show a content control in the in-app dialog host if available. Falls back to null if not available.
    /// </summary>
    public async Task<TResult?> ShowContentDialogAsync<TResult>(Control content)
    {
        if (InAppDialogHost.Instance != null)
        {
            var obj = await InAppDialogHost.Instance.ShowDialogAsync<object?>(content);
            return (TResult?)obj;
        }

        return default;
    }

    /// <summary>
    /// Show an open file dialog
    /// </summary>
    public async Task<string[]?> ShowOpenFileDialogAsync(string title, params string[] filters)
    {
        var mainWindow = GetMainWindow();
        if (mainWindow == null)
            return null;

        var storage = mainWindow.StorageProvider;
        if (storage != null)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var optionsType = assemblies.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "FilePickerOpenOptions");
                if (optionsType != null)
                {
                    var options = Activator.CreateInstance(optionsType);
                    var titleProp = optionsType.GetProperty("Title");
                    var allowMultiProp = optionsType.GetProperty("AllowMultiple");
                    titleProp?.SetValue(options, title);
                    allowMultiProp?.SetValue(options, false);

                    var method = storage.GetType().GetMethod("OpenFilePickerAsync", new[] { optionsType });
                    if (method != null)
                    {
                        var task = (Task)method.Invoke(storage, new[] { options })!;
                        await task.ConfigureAwait(false);
                        var resultProp = task.GetType().GetProperty("Result");
                        var filesObj = resultProp?.GetValue(task) as System.Collections.IEnumerable;
                        if (filesObj == null) return null;

                        var results = new List<string>();
                        foreach (var f in filesObj)
                        {
                            try
                            {
                                var fType = f.GetType();
                                var tryGetLocal = fType.GetMethod("TryGetLocalPath");
                                if (tryGetLocal != null)
                                {
                                    var args = new object?[] { null };
                                    var ok = (bool)tryGetLocal.Invoke(f, args)!;
                                    if (ok && args[0] is string local && !string.IsNullOrEmpty(local))
                                    {
                                        results.Add(local);
                                        continue;
                                    }
                                }

                                var pathProp = fType.GetProperty("Path") ?? fType.GetProperty("FullPath") ?? fType.GetProperty("Name");
                                if (pathProp != null)
                                {
                                    var val = pathProp.GetValue(f) as string;
                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        results.Add(val);
                                        continue;
                                    }
                                }

                                // try OpenReadAsync
                                var openRead = fType.GetMethod("OpenReadAsync");
                                if (openRead != null)
                                {
                                    var readTask = (Task)openRead.Invoke(f, Array.Empty<object>())!;
                                    await readTask.ConfigureAwait(false);
                                    var streamProp = readTask.GetType().GetProperty("Result");
                                    var stream = streamProp?.GetValue(readTask) as System.IO.Stream;
                                    if (stream != null)
                                    {
                                        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                                        using var fs = File.Create(tmp);
                                        await stream.CopyToAsync(fs);
                                        results.Add(tmp);
                                        continue;
                                    }
                                }
                            }
                            catch { }
                        }

                        return results.Count > 0 ? results.ToArray() : null;
                    }
                }
            }
            catch { }
        }

        // Fallback to OpenFileDialog (older API)
        var dialog = new OpenFileDialog
        {
            Title = title,
            AllowMultiple = false
        };

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

        var storage = mainWindow.StorageProvider;
        if (storage != null)
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var optionsType = assemblies.SelectMany(a => a.GetTypes()).FirstOrDefault(t => t.Name == "FilePickerSaveOptions");
                if (optionsType != null)
                {
                    var options = Activator.CreateInstance(optionsType);
                    var titleProp = optionsType.GetProperty("Title");
                    var defaultNameProp = optionsType.GetProperty("DefaultFileName");
                    titleProp?.SetValue(options, title);
                    defaultNameProp?.SetValue(options, "");

                    var method = storage.GetType().GetMethod("SaveFilePickerAsync", new[] { optionsType });
                    if (method != null)
                    {
                        var task = (Task)method.Invoke(storage, new[] { options })!;
                        await task.ConfigureAwait(false);
                        var resultProp = task.GetType().GetProperty("Result");
                        var f = resultProp?.GetValue(task);
                        if (f == null) return null;

                        try
                        {
                            var fType = f.GetType();
                            var tryGetLocal = fType.GetMethod("TryGetLocalPath");
                            if (tryGetLocal != null)
                            {
                                var args = new object?[] { null };
                                var ok = (bool)tryGetLocal.Invoke(f, args)!;
                                if (ok && args[0] is string local && !string.IsNullOrEmpty(local))
                                    return local;
                            }

                            var pathProp = fType.GetProperty("Path") ?? fType.GetProperty("FullPath") ?? fType.GetProperty("Name");
                            if (pathProp != null)
                            {
                                var val = pathProp.GetValue(f) as string;
                                if (!string.IsNullOrEmpty(val))
                                    return val;
                            }

                            var openRead = fType.GetMethod("OpenReadAsync");
                            if (openRead != null)
                            {
                                var readTask = (Task)openRead.Invoke(f, Array.Empty<object>())!;
                                await readTask.ConfigureAwait(false);
                                var streamProp = readTask.GetType().GetProperty("Result");
                                var stream = streamProp?.GetValue(readTask) as System.IO.Stream;
                                if (stream != null)
                                {
                                    var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + defaultExtension);
                                    using var fs = File.Create(tmp);
                                    await stream.CopyToAsync(fs);
                                    return tmp;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // Fallback to SaveFileDialog
        var dialog = new SaveFileDialog
        {
            Title = title,
            DefaultExtension = defaultExtension
        };

        return await dialog.ShowAsync(mainWindow);
    }
}
