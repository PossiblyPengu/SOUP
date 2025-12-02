using System.Windows;
using System.Windows.Controls;
using SAP.ViewModels;

namespace SAP.Views.SwiftLabel;

public partial class SwiftLabelView : UserControl
{
    public SwiftLabelView()
    {
        InitializeComponent();
    }

    private void IncrementBoxes_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SwiftLabelViewModel vm)
        {
            vm.TotalBoxes++;
        }
    }

    private void DecrementBoxes_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SwiftLabelViewModel vm && vm.TotalBoxes > 1)
        {
            vm.TotalBoxes--;
        }
    }
}
