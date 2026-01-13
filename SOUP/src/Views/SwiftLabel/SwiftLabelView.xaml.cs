using System.Windows;
using System.Windows.Controls;
using SOUP.ViewModels;

namespace SOUP.Views.SwiftLabel;

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

    private void SetBoxCount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagValue &&
            int.TryParse(tagValue, out int count) &&
            DataContext is SwiftLabelViewModel vm)
        {
            vm.TotalBoxes = count;
        }
    }
}
