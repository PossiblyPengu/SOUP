using System.Configuration;
using System.Data;
using System.Windows;

namespace MechaRogue;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Unhandled exception:\n{ex?.Message}\n\n{ex?.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        
        DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"Dispatcher exception:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

