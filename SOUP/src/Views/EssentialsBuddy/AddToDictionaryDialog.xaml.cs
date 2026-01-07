using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SOUP.Core.Entities.EssentialsBuddy;

namespace SOUP.Views.EssentialsBuddy;

/// <summary>
/// Dialog for adding unmatched items to the dictionary
/// </summary>
public partial class AddToDictionaryDialog : Window
{
    public List<InventoryItem> Items { get; }
    public bool WasConfirmed { get; private set; }

    public AddToDictionaryDialog(List<InventoryItem> unmatchedItems)
    {
        InitializeComponent();
        
        Items = unmatchedItems;
        ItemsGrid.ItemsSource = Items;
        
        UpdateSummary();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click to maximize/restore
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void UpdateSummary()
    {
        var essentialCount = Items.Count(i => i.IsEssential);
        SummaryText.Text = $"{Items.Count} items to add ({essentialCount} marked as essential)";
    }

    private void SelectAllEssentials_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
        {
            item.IsEssential = true;
        }
        ItemsGrid.Items.Refresh();
        UpdateSummary();
    }

    private void ClearAllEssentials_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Items)
        {
            item.IsEssential = false;
        }
        ItemsGrid.Items.Refresh();
        UpdateSummary();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        WasConfirmed = false;
        DialogResult = false;
        Close();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        WasConfirmed = true;
        DialogResult = true;
        Close();
    }
}
