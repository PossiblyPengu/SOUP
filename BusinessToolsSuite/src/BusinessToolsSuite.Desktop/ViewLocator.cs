using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using BusinessToolsSuite.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BusinessToolsSuite.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        // If param is already a Control, return it directly
        if (param is Control control)
            return control;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);

        // Search all loaded assemblies for the type
        var type = Type.GetType(name);
        if (type == null)
        {
            type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name))
                .FirstOrDefault(t => t != null);
        }

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        // Match ViewModels and Controls (for direct view passing)
        return data is ViewModelBase || data is ObservableObject || data is Control;
    }
}
