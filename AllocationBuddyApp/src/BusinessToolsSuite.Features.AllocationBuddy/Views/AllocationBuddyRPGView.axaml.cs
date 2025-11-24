using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace BusinessToolsSuite.Features.AllocationBuddy.Views;

public partial class AllocationBuddyRPGView : UserControl
{
    private Border? _dropArea;
    private IBrush? _originalBackground;

    public AllocationBuddyRPGView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        // Wire drag/drop handlers programmatically (XAML event hookup caused schema issues)
        try
        {
            _dropArea = this.FindControl<Border>("DropArea");
            if (_dropArea != null)
            {
                _originalBackground = _dropArea.Background;
                _dropArea.AddHandler(DragDrop.DropEvent, OnFilesDropped, handledEventsToo: true);
                _dropArea.AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
            }
        }
        catch { }
    }

    private async void OnFilesDropped(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is null) return;
            // Try to extract file names from the drag data
            var data = e.Data;
            string[]? files = null;
            try
            {
                if (data.Contains(DataFormats.FileNames))
                {
                    var obj = data.Get(DataFormats.FileNames);
                    if (obj is IEnumerable<string> names)
                        files = names.ToArray();
                    else if (obj is string[] arr)
                        files = arr;
                }
            }
            catch { }

            if (files == null || files.Length == 0) return;

            // Call the strongly-typed ImportFilesCommand on the ViewModel if available
            try
            {
                if (DataContext is BusinessToolsSuite.Features.AllocationBuddy.ViewModels.AllocationBuddyRPGViewModel vm)
                {
                    // Execute the async command (fire-and-forget is fine for UI)
                    vm.ImportFilesCommand.Execute(files);
                    // optionally await a tick to let UI update
                    await Task.Yield();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error executing ImportFilesCommand: " + ex.Message);
            }
            finally
            {
                // restore visual state
                if (_dropArea != null) _dropArea.Background = _originalBackground;
            }
        }
        catch { }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        try
        {
            // Provide visual feedback by highlighting the drop area
            if (_dropArea != null)
            {
                _dropArea.Background = Brushes.LightBlue;
            }
            e.Handled = true;
        }
        catch { }
    }

    
}
