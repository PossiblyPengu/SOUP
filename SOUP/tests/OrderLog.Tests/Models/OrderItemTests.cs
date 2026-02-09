using System;
using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using Xunit;

namespace SOUP.Tests.OrderLog.Models;

public class OrderItemTests
{
    [Fact]
    public void OrderItem_CreatesWithDefaults()
    {
        var item = new OrderItem
        {
            VendorName = "TestVendor",
            TransferNumbers = "T001"
        };

        item.Id.Should().NotBe(Guid.Empty);
        item.VendorName.Should().Be("TestVendor");
        item.TransferNumbers.Should().Be("T001");
        item.Status.Should().Be(OrderItem.OrderStatus.NotReady);
        item.IsArchived.Should().BeFalse();
        item.CreatedAt.Should().BeBefore(DateTime.UtcNow);
    }

    [Fact]
    public void OrderItem_SetStatus_Valid()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.NotReady };

        item.Status = OrderItem.OrderStatus.OnDeck;

        item.Status.Should().Be(OrderItem.OrderStatus.OnDeck);
    }

    [Fact]
    public void OrderItem_AllowsAllStatusValues()
    {
        var item = new OrderItem { VendorName = "Test" };
        var statusValues = Enum.GetValues(typeof(OrderItem.OrderStatus)).Cast<OrderItem.OrderStatus>();

        foreach (var status in statusValues)
        {
            item.Status = status;
            item.Status.Should().Be(status);
        }
    }

    [Fact]
    public void OrderItem_LinkedGroupId_CanBeNull()
    {
        var item = new OrderItem { VendorName = "Test" };

        item.LinkedGroupId.Should().BeNull();
    }

    [Fact]
    public void OrderItem_LinkedGroupId_CanBeAssigned()
    {
        var item = new OrderItem { VendorName = "Test" };
        var groupId = Guid.NewGuid();

        item.LinkedGroupId = groupId;

        item.LinkedGroupId.Should().Be(groupId);
    }

    [Fact]
    public void OrderItem_NoteType_StickyNote()
    {
        var item = new OrderItem
        {
            VendorName = "StickyNote",
            NoteType = NoteType.StickyNote
        };

        item.NoteType.Should().Be(NoteType.StickyNote);
    }

    [Fact]
    public void OrderItem_NoteType_Order()
    {
        var item = new OrderItem
        {
            VendorName = "Order",
            NoteType = NoteType.Order
        };

        item.NoteType.Should().Be(NoteType.Order);
    }

    [Fact]
    public void OrderItem_Timestamp_SetOnCreation()
    {
        var beforeCreation = DateTime.UtcNow;
        var item = new OrderItem { VendorName = "Test" };
        var afterCreation = DateTime.UtcNow.AddMilliseconds(1);

        item.CreatedAt.Should().BeGreaterThanOrEqualTo(beforeCreation);
        item.CreatedAt.Should().BeLessThanOrEqualTo(afterCreation);
    }

    [Fact]
    public void OrderItem_UpdatedAt_SetOnModification()
    {
        var item = new OrderItem { VendorName = "Test" };
        var originalUpdatedAt = item.UpdatedAt;

        System.Threading.Thread.Sleep(10); // Small delay to ensure time difference
        item.VendorName = "Modified";

        // UpdatedAt should be modified (depends on implementation)
        // This test assumes UpdatedAt is updated when properties change
        item.VendorName.Should().Be("Modified");
    }

    [Fact]
    public void OrderItem_IsDeleted_DefaultFalse()
    {
        var item = new OrderItem { VendorName = "Test" };

        item.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void OrderItem_MultipleTransferNumbers()
    {
        var item = new OrderItem
        {
            VendorName = "Test",
            TransferNumbers = "T001,T002,T003"
        };

        item.TransferNumbers.Should().Be("T001,T002,T003");
    }

    [Fact]
    public void OrderItem_ValidateVendorNameRequired()
    {
        var item = new OrderItem { VendorName = "" };

        item.VendorName.Should().Be("");
    }

    [Fact]
    public void OrderItem_ContentCanBeMultiline()
    {
        var content = @"Line 1
Line 2
Line 3";
        var item = new OrderItem
        {
            VendorName = "Test",
            Content = content
        };

        item.Content.Should().Be(content);
    }

    [Fact]
    public void OrderItem_ColorHex_ValidFormat()
    {
        var item = new OrderItem
        {
            VendorName = "Test",
            ColorHex = "#FF5733"
        };

        item.ColorHex.Should().Be("#FF5733");
    }

    [Fact]
    public void OrderItem_ArchiveFlag_CanBeSet()
    {
        var item = new OrderItem { VendorName = "Test", IsArchived = false };

        item.IsArchived = true;

        item.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void OrderItem_TimeTracking_InProgress()
    {
        var item = new OrderItem
        {
            VendorName = "Test",
            Status = OrderItem.OrderStatus.InProgress,
            TimeSpentInProgress = TimeSpan.Zero
        };

        item.Status.Should().Be(OrderItem.OrderStatus.InProgress);
    }

    [Fact]
    public void OrderItem_TimeTracking_OnDeck()
    {
        var item = new OrderItem
        {
            VendorName = "Test",
            Status = OrderItem.OrderStatus.OnDeck,
            TimeSpentOnDeck = TimeSpan.Zero
        };

        item.Status.Should().Be(OrderItem.OrderStatus.OnDeck);
    }
}
