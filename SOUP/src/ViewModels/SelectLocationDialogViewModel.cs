using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SOUP.ViewModels;

public partial class SelectLocationDialogViewModel : ObservableObject
{
    public ObservableCollection<string> Locations { get; } = new();

    [ObservableProperty]
    private string? _selectedLocation;

    [ObservableProperty]
    private int _selectedQuantity = 1;

    public IRelayCommand OkCommand { get; }
    public IRelayCommand CancelCommand { get; }

    public SelectLocationDialogViewModel()
    {
        OkCommand = new RelayCommand(() => { });
        CancelCommand = new RelayCommand(() => { SelectedLocation = null; SelectedQuantity = 1; });
    }
}
