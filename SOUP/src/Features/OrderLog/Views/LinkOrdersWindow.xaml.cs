using System;
using System.Linq;
using System.Windows;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Views;

public partial class LinkOrdersWindow : Window
{
    private readonly OrderItem _source;
    private readonly OrderLogViewModel _vm;

    public OrderItem? SelectedOrder => OrdersList.SelectedItem as OrderItem;

    public LinkOrdersWindow(OrderItem source, OrderLogViewModel vm)
    {
        InitializeComponent();
        _source = source;
        _vm = vm;

        // Populate list with non-archived orders excluding the source
        OrdersList.ItemsSource = _vm.Items.Where(i => i.Id != _source.Id && i.NoteType == NoteType.Order).ToList();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void LinkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (OrdersList.SelectedItem is not OrderItem target)
        {
            MessageBox.Show(this, "Please select an order to link with.", "Link Orders", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Determine group id to use: prefer existing group id from either item
        Guid groupId = _source.LinkedGroupId ?? target.LinkedGroupId ?? Guid.NewGuid();

        // Apply the group to both (and to any other items in either group)
        foreach (var item in _vm.Items)
        {
            if (item.Id == _source.Id || item.Id == target.Id) item.LinkedGroupId = groupId;
            else if (item.LinkedGroupId != null && (item.LinkedGroupId == _source.LinkedGroupId || item.LinkedGroupId == target.LinkedGroupId))
            {
                item.LinkedGroupId = groupId; // unify groups
            }
        }

        // Also update archived collection if needed
        foreach (var item in _vm.ArchivedItems)
        {
            if (item.Id == _source.Id || item.Id == target.Id) item.LinkedGroupId = groupId;
            else if (item.LinkedGroupId != null && (item.LinkedGroupId == _source.LinkedGroupId || item.LinkedGroupId == target.LinkedGroupId))
            {
                item.LinkedGroupId = groupId;
            }
        }

        DialogResult = true;
        Close();
    }
}
