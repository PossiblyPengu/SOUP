using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BusinessToolsSuite.Features.AllocationBuddy.ViewModels;
using BusinessToolsSuite.Features.AllocationBuddy.Views;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        // Get ViewModel from DI container
        var viewModel = Program.AppHost?.Services.GetRequiredService<AllocationBuddyRPGViewModel>();

        var view = new AllocationBuddyRPGView
        {
            DataContext = viewModel
        };

        this.Content = view;
        this.Title = "Allocation Buddy";
        this.Width = 1200;
        this.Height = 800;
    }
}
