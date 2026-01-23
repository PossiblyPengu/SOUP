using System.Windows;
using System.Windows.Input;
using SOUP.ViewModels;

namespace SOUP.Features.ExpireWise.Helpers;

/// <summary>
/// Manages keyboard shortcuts for the ExpireWise module.
/// </summary>
public class KeyboardShortcutManager
{
    /// <summary>
    /// Registers keyboard shortcuts for the ExpireWise view.
    /// </summary>
    /// <param name="element">The UI element to register shortcuts on (typically the View).</param>
    /// <param name="viewModel">The ExpireWiseViewModel to invoke commands on.</param>
    /// <param name="focusSearchAction">Optional action to focus the search box (Ctrl+F).</param>
    public void RegisterShortcuts(
        UIElement element,
        ExpireWiseViewModel viewModel,
        Action? focusSearchAction = null)
    {
        element.PreviewKeyDown += (s, e) =>
        {
            var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

            switch (e.Key)
            {
                // Ctrl+N: Add new item
                case Key.N when ctrl && !shift:
                    if (viewModel.AddItemCommand.CanExecute(null))
                    {
                        viewModel.AddItemCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+E: Edit selected item
                case Key.E when ctrl && !shift:
                    if (viewModel.EditItemCommand.CanExecute(null))
                    {
                        viewModel.EditItemCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Delete: Delete selected item
                case Key.Delete when ctrl:
                    if (viewModel.DeleteItemCommand.CanExecute(null))
                    {
                        viewModel.DeleteItemCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Shift+E: Export to Excel
                case Key.E when ctrl && shift:
                    if (viewModel.ExportToExcelCommand.CanExecute(null))
                    {
                        viewModel.ExportToExcelCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Shift+C: Export to CSV
                case Key.C when ctrl && shift:
                    if (viewModel.ExportToCsvCommand.CanExecute(null))
                    {
                        viewModel.ExportToCsvCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Shift+I: Import from CSV
                case Key.I when ctrl && shift:
                    if (viewModel.ImportFromCsvCommand.CanExecute(null))
                    {
                        viewModel.ImportFromCsvCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+F: Focus search box
                case Key.F when ctrl:
                    focusSearchAction?.Invoke();
                    e.Handled = true;
                    break;

                // Escape: Clear search
                case Key.Escape:
                    if (!string.IsNullOrEmpty(viewModel.SearchText))
                    {
                        viewModel.SearchText = string.Empty;
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Left Arrow: Previous month
                case Key.Left when ctrl:
                    if (viewModel.PreviousMonthCommand.CanExecute(null))
                    {
                        viewModel.PreviousMonthCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+Right Arrow: Next month
                case Key.Right when ctrl:
                    if (viewModel.NextMonthCommand.CanExecute(null))
                    {
                        viewModel.NextMonthCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                // Ctrl+T: Go to today's month (TODO: Implement TodayCommand)
                // case Key.T when ctrl:
                //     if (viewModel.TodayCommand.CanExecute(null))
                //     {
                //         viewModel.TodayCommand.Execute(null);
                //         e.Handled = true;
                //     }
                //     break;

                // Ctrl+R: Refresh/reload items
                case Key.R when ctrl:
                    if (viewModel.LoadItemsCommand.CanExecute(null))
                    {
                        viewModel.LoadItemsCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        };
    }
}
