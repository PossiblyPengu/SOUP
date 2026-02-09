using System;
using System.Collections.Generic;
using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Services;
using Xunit;

namespace SOUP.Tests.OrderLog.Services;

public class OrderSearchServiceTests
{
    [Fact]
    public void OrderSearchService_HasActiveFilters_NoneActive()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null);

        hasFilters.Should().BeFalse();
    }

    [Fact]
    public void OrderSearchService_HasActiveFilters_SearchQuery()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "test",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null);

        hasFilters.Should().BeTrue();
    }

    [Fact]
    public void OrderSearchService_HasActiveFilters_StatusFilter()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "",
            statusFilters: new[] { OrderItem.OrderStatus.OnDeck },
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null);

        hasFilters.Should().BeTrue();
    }

    [Fact]
    public void OrderSearchService_HasActiveFilters_DateRange()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "",
            statusFilters: null,
            filterStartDate: DateTime.Now.AddDays(-7),
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null);

        hasFilters.Should().BeTrue();
    }

    [Fact]
    public void OrderSearchService_HasActiveFilters_ColorFilter()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: new[] { "#FF5733" },
            noteTypeFilter: null,
            noteCategoryFilter: null);

        hasFilters.Should().BeTrue();
    }

    [Fact]
    public void OrderSearchService_HasActiveFilters_NoteTypeFilter()
    {
        var service = new OrderSearchService();

        var hasFilters = service.HasActiveFilters(
            searchQuery: "",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: NoteType.StickyNote,
            noteCategoryFilter: null);

        hasFilters.Should().BeTrue();
    }

    [Fact]
    public void OrderSearchService_ApplySearchFilter_ByVendorName()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "Acme Corp", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Best Inc", Status = OrderItem.OrderStatus.OnDeck },
            new() { VendorName = "Amazing Ltd", Status = OrderItem.OrderStatus.InProgress }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "acme",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(1);
        result[0].VendorName.Should().Be("Acme Corp");
    }

    [Fact]
    public void OrderSearchService_ApplyStatusFilter()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "A", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "B", Status = OrderItem.OrderStatus.OnDeck },
            new() { VendorName = "C", Status = OrderItem.OrderStatus.InProgress }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "",
            statusFilters: new[] { OrderItem.OrderStatus.OnDeck },
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(1);
        result[0].VendorName.Should().Be("B");
    }

    [Fact]
    public void OrderSearchService_ApplyColorFilter()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "Red", ColorHex = "#FF0000", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Blue", ColorHex = "#0000FF", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Red2", ColorHex = "#FF0000", Status = OrderItem.OrderStatus.NotReady }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: new[] { "#FF0000" },
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(i => i.ColorHex.Should().Be("#FF0000"));
    }

    [Fact]
    public void OrderSearchService_ApplyDateRangeFilter()
    {
        var now = DateTime.UtcNow;
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "Old", Status = OrderItem.OrderStatus.NotReady, CreatedAt = now.AddDays(-10) },
            new() { VendorName = "Recent", Status = OrderItem.OrderStatus.NotReady, CreatedAt = now.AddDays(-1) },
            new() { VendorName = "VeryOld", Status = OrderItem.OrderStatus.NotReady, CreatedAt = now.AddDays(-30) }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "",
            statusFilters: null,
            filterStartDate: now.AddDays(-7),
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(1);
        result[0].VendorName.Should().Be("Recent");
    }

    [Fact]
    public void OrderSearchService_ApplyNoteTypeFilter()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "Order", NoteType = NoteType.Order, Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Sticky", NoteType = NoteType.StickyNote, Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Order2", NoteType = NoteType.Order, Status = OrderItem.OrderStatus.NotReady }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: NoteType.StickyNote,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(1);
        result[0].VendorName.Should().Be("Sticky");
    }

    [Fact]
    public void OrderSearchService_CombineFilters()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "Vendor A", Status = OrderItem.OrderStatus.OnDeck, ColorHex = "#FF0000", NoteType = NoteType.Order },
            new() { VendorName = "Vendor B", Status = OrderItem.OrderStatus.OnDeck, ColorHex = "#0000FF", NoteType = NoteType.Order },
            new() { VendorName = "Vendor A2", Status = OrderItem.OrderStatus.NotReady, ColorHex = "#FF0000", NoteType = NoteType.Order }
        };

        // Filter by vendor name AND status AND color
        var result = service.ApplyAllFilters(
            items,
            searchQuery: "vendor a",
            statusFilters: new[] { OrderItem.OrderStatus.OnDeck },
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: new[] { "#FF0000" },
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(1);
        result[0].VendorName.Should().Be("Vendor A");
    }

    [Fact]
    public void OrderSearchService_EmptyResultWhenNoMatches()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "A", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "B", Status = OrderItem.OrderStatus.OnDeck }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "xyz",
            statusFilters: null,
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void OrderSearchService_MultipleStatusFilters()
    {
        var service = new OrderSearchService();
        var items = new List<OrderItem>
        {
            new() { VendorName = "A", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "B", Status = OrderItem.OrderStatus.OnDeck },
            new() { VendorName = "C", Status = OrderItem.OrderStatus.InProgress }
        };

        var result = service.ApplyAllFilters(
            items,
            searchQuery: "",
            statusFilters: new[] { OrderItem.OrderStatus.OnDeck, OrderItem.OrderStatus.InProgress },
            filterStartDate: null,
            filterEndDate: null,
            colorFilters: null,
            noteTypeFilter: null,
            noteCategoryFilter: null).ToList();

        result.Should().HaveCount(2);
        result.Select(r => r.VendorName).Should().BeEquivalentTo("B", "C");
    }
}
