using Avalonia.Controls;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        // Try to host the feature view; if that fails, show an empty Window
        try
        {
            var view = new BusinessToolsSuite.Features.AllocationBuddy.Views.AllocationBuddyRPGView();
            this.Content = view;
            this.Title = "Allocation Buddy";
            this.Width = 1200;
            this.Height = 800;
        }
        catch
        {
            this.Title = "Allocation Buddy (no view)";
        }
    }
}
