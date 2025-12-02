using System.Windows.Controls;
using SAP.ViewModels;

namespace SAP.Views.AllocationBuddy;

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
}
