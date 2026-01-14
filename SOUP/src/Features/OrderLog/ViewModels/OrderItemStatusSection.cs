using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.ViewModels;

public partial class OrderItemStatusSection : ObservableObject
{
    public OrderItem.OrderStatus Status { get; }
    public ObservableCollection<OrderItemGroup> Groups { get; } = new();

    [ObservableProperty]
    private bool _isExpanded = true;

    public string Header => Status.ToString();

    public OrderItemStatusSection(OrderItem.OrderStatus status)
    {
        Status = status;
    }
}
