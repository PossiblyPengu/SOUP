using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.Views;

public partial class OrderLogFilterDialog : Window
{
    public OrderItem.OrderStatus[]? SelectedStatuses { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public NoteType? SelectedNoteType { get; private set; }

    public OrderLogFilterDialog(
        OrderItem.OrderStatus[]? currentStatuses = null,
        DateTime? currentStartDate = null,
        DateTime? currentEndDate = null,
        NoteType? currentNoteType = null)
    {
        InitializeComponent();

        // Set initial values from current filters
        if (currentStatuses != null)
        {
            StatusNotReadyCheckBox.IsChecked = currentStatuses.Contains(OrderItem.OrderStatus.NotReady);
            StatusOnDeckCheckBox.IsChecked = currentStatuses.Contains(OrderItem.OrderStatus.OnDeck);
            StatusInProgressCheckBox.IsChecked = currentStatuses.Contains(OrderItem.OrderStatus.InProgress);
            StatusDoneCheckBox.IsChecked = currentStatuses.Contains(OrderItem.OrderStatus.Done);
        }

        StartDatePicker.SelectedDate = currentStartDate;
        EndDatePicker.SelectedDate = currentEndDate;

        if (currentNoteType.HasValue)
        {
            TypeAllRadio.IsChecked = false;
            if (currentNoteType == NoteType.Order)
                TypeOrdersOnlyRadio.IsChecked = true;
            else
                TypeNotesOnlyRadio.IsChecked = true;
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        // Collect selected statuses
        var statuses = new List<OrderItem.OrderStatus>();
        if (StatusNotReadyCheckBox.IsChecked == true)
            statuses.Add(OrderItem.OrderStatus.NotReady);
        if (StatusOnDeckCheckBox.IsChecked == true)
            statuses.Add(OrderItem.OrderStatus.OnDeck);
        if (StatusInProgressCheckBox.IsChecked == true)
            statuses.Add(OrderItem.OrderStatus.InProgress);
        if (StatusDoneCheckBox.IsChecked == true)
            statuses.Add(OrderItem.OrderStatus.Done);

        SelectedStatuses = statuses.Count > 0 ? statuses.ToArray() : null;

        // Date range
        StartDate = StartDatePicker.SelectedDate;
        EndDate = EndDatePicker.SelectedDate;

        // Note type
        if (TypeOrdersOnlyRadio.IsChecked == true)
            SelectedNoteType = NoteType.Order;
        else if (TypeNotesOnlyRadio.IsChecked == true)
            SelectedNoteType = NoteType.StickyNote;
        else
            SelectedNoteType = null;

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        // Uncheck all status checkboxes
        StatusNotReadyCheckBox.IsChecked = false;
        StatusOnDeckCheckBox.IsChecked = false;
        StatusInProgressCheckBox.IsChecked = false;
        StatusDoneCheckBox.IsChecked = false;

        // Clear date pickers
        StartDatePicker.SelectedDate = null;
        EndDatePicker.SelectedDate = null;

        // Reset to "All" for note type
        TypeAllRadio.IsChecked = true;
    }
}
