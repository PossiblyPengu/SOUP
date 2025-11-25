using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using BusinessToolsSuite.Features.EssentialsBuddy.ViewModels;
using BusinessToolsSuite.Features.EssentialsBuddy.Views;

namespace BusinessToolsSuite.Desktop;

public class MainWindow : Window
{
    public MainWindow()
    {
        // Get ViewModel from DI container
        var viewModel = Program.AppHost?.Services.GetRequiredService<EssentialsBuddyViewModel>();

        var view = new EssentialsBuddyView
        {
            DataContext = viewModel
        };

        this.Content = view;
        this.Title = "Essentials Buddy";
        this.Width = 1200;
        this.Height = 800;
    }
}
