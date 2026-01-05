using System.Collections.ObjectModel;
using System.Linq;
using SOUP.Features.OrderLog.Models;

namespace SOUP.Features.OrderLog.ViewModels;

public class OrderItemGroup
{
    public ObservableCollection<OrderItem> Members { get; } = new();

    public OrderItem? First => Members.FirstOrDefault();

    public int Count => Members.Count;

    public bool IsGroup => Count > 1;

    public Guid? LinkedGroupId => First?.LinkedGroupId;

    public OrderItemGroup() { }

    public OrderItemGroup(IEnumerable<OrderItem> items)
    {
        foreach (var it in items)
            Members.Add(it);
    }
}
