using System.Windows;
using System.Windows.Controls;
using SOUP.ViewModels;

namespace SOUP.Views.AllocationBuddy;

public partial class AllocationBuddyRPGView : UserControl
{
    public AllocationBuddyRPGView()
    {
        InitializeComponent();
    }

    public AllocationBuddyRPGView(AllocationBuddyRPGViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void CloseArchivePanel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AllocationBuddyRPGViewModel vm)
        {
            vm.IsArchivePanelOpen = false;
        }
    }
}
