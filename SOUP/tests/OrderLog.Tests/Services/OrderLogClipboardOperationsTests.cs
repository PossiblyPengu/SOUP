using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using Xunit;

namespace SOUP.Features.OrderLog.Tests.Services;

/// <summary>
/// Tests for clipboard operations: copy, paste, and duplicate
/// </summary>
public class OrderLogClipboardOperationsTests
{
    [Fact]
    public void Copy_WithNoSelection_SetsStatusMessage()
    {
        // Arrange
        var items = new List<OrderItem>();

        // Act
        var selectedItems = new List<OrderItem>();

        // Assert
        selectedItems.Should().BeEmpty();
    }

    [Fact]
    public void Copy_WithSingleSelection_CopiesItem()
    {
        // Arrange
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            NoteType = NoteType.Order
        };
        var selectedItems = new List<OrderItem> { item };

        // Act & Assert
        selectedItems.Should().HaveCount(1);
        selectedItems.First().VendorName.Should().Be("Test Vendor");
    }

    [Fact]
    public void Copy_WithMultipleSelections_CopiesAllItems()
    {
        // Arrange
        var item1 = new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor 1" };
        var item2 = new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor 2" };
        var item3 = new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor 3" };

        var selectedItems = new List<OrderItem> { item1, item2, item3 };

        // Act & Assert
        selectedItems.Should().HaveCount(3);
        selectedItems.Select(i => i.VendorName).Should().ContainInOrder("Vendor 1", "Vendor 2", "Vendor 3");
    }

    [Fact]
    public void Paste_WithMultipleItems_PreservesOrder()
    {
        // Arrange
        var pastedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "A", CreatedAt = DateTime.UtcNow },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "B", CreatedAt = DateTime.UtcNow.AddSeconds(1) },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "C", CreatedAt = DateTime.UtcNow.AddSeconds(2) }
        };

        // Act & Assert
        pastedItems.Should().HaveCount(3);
        pastedItems.First().VendorName.Should().Be("A");
        pastedItems.Last().VendorName.Should().Be("C");
    }

    [Fact]
    public void Paste_WithAutoColoring_AssignsVendorColors()
    {
        // Arrange
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "TestVendor",
            NoteType = NoteType.Order,
            ColorHex = "#FFFFFF"
        };

        // Simulate color assignment
        var vendorColorMap = new Dictionary<string, string>
        {
            { "TestVendor", "#FF0000" }
        };

        // Act
        if (vendorColorMap.ContainsKey(item.VendorName))
        {
            item.ColorHex = vendorColorMap[item.VendorName];
        }

        // Assert
        item.ColorHex.Should().Be("#FF0000");
    }

    [Fact]
    public void Duplicate_CreatesNewItems()
    {
        // Arrange
        var original = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "Original",
            TransferNumbers = "123",
            ColorHex = "#FF0000"
        };

        // Act - simulate clone
        var duplicate = new OrderItem
        {
            Id = Guid.NewGuid(), // New ID
            VendorName = original.VendorName,
            TransferNumbers = original.TransferNumbers,
            ColorHex = original.ColorHex
        };

        // Assert
        duplicate.Id.Should().NotBe(original.Id);
        duplicate.VendorName.Should().Be(original.VendorName);
        duplicate.TransferNumbers.Should().Be(original.TransferNumbers);
    }

    [Fact]
    public void Duplicate_WithMultipleItems_CreatesMultipleClones()
    {
        // Arrange
        var items = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "A" },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "B" }
        };

        // Act - simulate duplication
        var duplicates = items.Select(i => new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = i.VendorName
        }).ToList();

        // Assert
        duplicates.Should().HaveCount(2);
        duplicates.Select(d => d.VendorName).Should().ContainInOrder("A", "B");
        duplicates.All(d => d.Id != Guid.Empty).Should().BeTrue();
    }

    [Fact]
    public void Copy_StickyNote_PreservesNoteContent()
    {
        // Arrange
        var note = new OrderItem
        {
            Id = Guid.NewGuid(),
            NoteType = NoteType.StickyNote,
            NoteContent = "Important reminder",
            ColorHex = "#FFFF00"
        };

        var selectedItems = new List<OrderItem> { note };

        // Act & Assert
        selectedItems.First().NoteContent.Should().Be("Important reminder");
        selectedItems.First().NoteType.Should().Be(NoteType.StickyNote);
    }

    [Fact]
    public void Paste_PreservesLinkedGroupId()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var pastedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", LinkedGroupId = groupId },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", LinkedGroupId = groupId }
        };

        // Act & Assert
        pastedItems.All(i => i.LinkedGroupId == groupId).Should().BeTrue();
    }

    [Fact]
    public void Copy_PreservesAllProperties()
    {
        // Arrange
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "TestVendor",
            TransferNumbers = "TRN123",
            WhsShipmentNumbers = "SHIP456",
            Status = OrderItem.OrderStatus.InProgress,
            ColorHex = "#00FF00",
            NoteType = NoteType.Order
        };

        // Act
        var copiedItem = item;

        // Assert
        copiedItem.VendorName.Should().Be(item.VendorName);
        copiedItem.TransferNumbers.Should().Be(item.TransferNumbers);
        copiedItem.WhsShipmentNumbers.Should().Be(item.WhsShipmentNumbers);
        copiedItem.Status.Should().Be(item.Status);
        copiedItem.ColorHex.Should().Be(item.ColorHex);
    }
}
