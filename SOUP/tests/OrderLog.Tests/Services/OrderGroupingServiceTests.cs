using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Services;
using SOUP.Features.OrderLog.ViewModels;
using Xunit;

namespace SOUP.Tests.OrderLog.Services;

public class OrderGroupingServiceTests
{
    [Fact]
    public void BuildDisplayCollection_GroupsLinkedItemsOnce()
    {
        var groupId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "A",
                LinkedGroupId = groupId,
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                VendorName = "B",
                LinkedGroupId = groupId,
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: false, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().HaveCount(1);
        result[0].Members.Should().HaveCount(2);
        result[0].Members.Select(m => m.VendorName).Should().BeEquivalentTo(new[] { "A", "B" });
    }

    [Fact]
    public void BuildDisplayCollection_SortsByStatusAscending()
    {
        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "Late",
                Status = OrderItem.OrderStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new()
            {
                VendorName = "First",
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            }
        };

        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: false, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().HaveCount(2);
        result[0].First?.VendorName.Should().Be("First");
        result[1].First?.VendorName.Should().Be("Late");
    }

    [Fact]
    public void PopulateStatusGroups_PlacesGroupsByStatus()
    {
        var notReady = new ObservableCollection<OrderItemGroup>();
        var onDeck = new ObservableCollection<OrderItemGroup>();
        var inProgress = new ObservableCollection<OrderItemGroup>();

        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "NR",
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new()
            {
                VendorName = "OD",
                Status = OrderItem.OrderStatus.OnDeck,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                VendorName = "IP",
                Status = OrderItem.OrderStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var service = new OrderGroupingService();

        service.PopulateStatusGroups(items, notReady, onDeck, inProgress);

        notReady.Should().HaveCount(1);
        onDeck.Should().HaveCount(1);
        inProgress.Should().HaveCount(1);
        notReady[0].First?.VendorName.Should().Be("NR");
        onDeck[0].First?.VendorName.Should().Be("OD");
        inProgress[0].First?.VendorName.Should().Be("IP");
    }

    [Fact]
    public void BuildDisplayCollection_HandlesEmptyCollection()
    {
        var items = new List<OrderItem>();
        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: false, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildDisplayCollection_SortsByStatusDescending()
    {
        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "First",
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                VendorName = "Second",
                Status = OrderItem.OrderStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: true, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().HaveCount(2);
        // InProgress should come before NotReady in descending order
        result[0].First?.VendorName.Should().Be("Second");
        result[1].First?.VendorName.Should().Be("First");
    }

    [Fact]
    public void BuildDisplayCollection_SingleItemGroup()
    {
        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "Single",
                Status = OrderItem.OrderStatus.OnDeck,
                CreatedAt = DateTime.UtcNow
            }
        };

        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: false, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().HaveCount(1);
        result[0].Members.Should().HaveCount(1);
        result[0].First?.VendorName.Should().Be("Single");
    }

    [Fact]
    public void BuildDisplayCollection_MultipleLinkedGroups()
    {
        var group1Id = Guid.NewGuid();
        var group2Id = Guid.NewGuid();

        var items = new List<OrderItem>
        {
            new()
            {
                VendorName = "GroupA-1",
                LinkedGroupId = group1Id,
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-3)
            },
            new()
            {
                VendorName = "GroupA-2",
                LinkedGroupId = group1Id,
                Status = OrderItem.OrderStatus.NotReady,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2)
            },
            new()
            {
                VendorName = "GroupB-1",
                LinkedGroupId = group2Id,
                Status = OrderItem.OrderStatus.OnDeck,
                CreatedAt = DateTime.UtcNow.AddMinutes(-1)
            }
        };

        var service = new OrderGroupingService();

        var result = service.BuildDisplayCollection(items, sortByStatus: true, sortStatusDescending: false, OrderGroupingService.OrderLogSortMode.Status);

        result.Should().HaveCount(2);
        result[0].Members.Should().HaveCount(2);
        result[1].Members.Should().HaveCount(1);
    }

    [Fact]
    public void PopulateStatusGroups_HandlesEmptyCollections()
    {
        var notReady = new ObservableCollection<OrderItemGroup>();
        var onDeck = new ObservableCollection<OrderItemGroup>();
        var inProgress = new ObservableCollection<OrderItemGroup>();

        var items = new List<OrderItem>();
        var service = new OrderGroupingService();

        // Should not throw
        service.PopulateStatusGroups(items, notReady, onDeck, inProgress);

        notReady.Should().BeEmpty();
        onDeck.Should().BeEmpty();
        inProgress.Should().BeEmpty();
    }

    [Fact]
    public void PopulateStatusGroups_MixedStatuses()
    {
        var notReady = new ObservableCollection<OrderItemGroup>();
        var onDeck = new ObservableCollection<OrderItemGroup>();
        var inProgress = new ObservableCollection<OrderItemGroup>();

        var items = new List<OrderItem>
        {
            new() { VendorName = "NR1", Status = OrderItem.OrderStatus.NotReady, CreatedAt = DateTime.UtcNow },
            new() { VendorName = "NR2", Status = OrderItem.OrderStatus.NotReady, CreatedAt = DateTime.UtcNow },
            new() { VendorName = "OD1", Status = OrderItem.OrderStatus.OnDeck, CreatedAt = DateTime.UtcNow },
            new() { VendorName = "IP1", Status = OrderItem.OrderStatus.InProgress, CreatedAt = DateTime.UtcNow }
        };

        var service = new OrderGroupingService();
        service.PopulateStatusGroups(items, notReady, onDeck, inProgress);

        notReady.Should().HaveCount(2);
        onDeck.Should().HaveCount(1);
        inProgress.Should().HaveCount(1);
    }
}
