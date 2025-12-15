using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using SAP.ViewModels;

namespace SAP.Views;

/// <summary>
/// External data connection settings view
/// </summary>
public partial class ExternalDataSettingsView : UserControl
{
    public ExternalDataSettingsView()
    {
        InitializeComponent();
        DataContext = App.GetService<ExternalDataViewModel>();
    }
}
