using MechaRogue.ViewModels;

namespace MechaRogue;

public partial class MainWindow : Window
{
    private readonly GameViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new GameViewModel();
        DataContext = _vm;

        // Auto-scroll battle log
        _vm.BattleLog.CollectionChanged += (_, _) =>
        {
            if (BattleLogBox.Items.Count > 0)
                BattleLogBox.ScrollIntoView(BattleLogBox.Items[^1]);
        };

        // Play battlefield animations on each resolved action
        _vm.OnActionResolved += result => Dispatcher.Invoke(() =>
        {
            BattleArena.PlayActionResult(result);
            RefreshBindings();
        });

        // Redraw battlefield when screen changes (e.g. entering battle)
        _vm.OnScreenChanged += screen => Dispatcher.Invoke(() =>
        {
            if (screen == "Battle")
                BattleArena.RedrawScene();
            RefreshBindings();
        });
    }

    private void RefreshBindings()
    {
        // Re-trigger binding updates for the deep model objects
        DataContext = null;
        DataContext = _vm;
    }
}
