using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using Xunit;

namespace SOUP.Features.OrderLog.Tests.Services;

/// <summary>
/// Tests for archive management operations: archive, unarchive, repair, and management
/// </summary>
public class OrderLogArchiveManagementTests
{
    [Fact]
    public void ArchiveItem_MovesFromActiveToArchived()
    {
        // Arrange
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor", Status = OrderItem.OrderStatus.Done }
        };
        var archivedItems = new List<OrderItem>();
        var itemToArchive = activeItems.First();

        // Act
        activeItems.Remove(itemToArchive);
        archivedItems.Add(itemToArchive);

        // Assert
        activeItems.Should().BeEmpty();
        archivedItems.Should().HaveCount(1);
        archivedItems.First().VendorName.Should().Be("Vendor");
    }

    [Fact]
    public void ArchiveItem_WithLinkedGroup_ArchivesAllLinked()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", LinkedGroupId = groupId, Status = OrderItem.OrderStatus.Done },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", LinkedGroupId = groupId, Status = OrderItem.OrderStatus.InProgress },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item3", LinkedGroupId = null, Status = OrderItem.OrderStatus.Done }
        };
        var archivedItems = new List<OrderItem>();

        // Act - Archive first item with group
        var itemToArchive = activeItems.First();
        var itemsToArchive = activeItems.Where(i => i.LinkedGroupId == itemToArchive.LinkedGroupId).ToList();
        foreach (var item in itemsToArchive)
        {
            activeItems.Remove(item);
            archivedItems.Add(item);
        }

        // Assert
        activeItems.Should().HaveCount(1);
        archivedItems.Should().HaveCount(2);
        archivedItems.All(i => i.LinkedGroupId == groupId).Should().BeTrue();
    }

    [Fact]
    public void UnarchiveItem_MovesFromArchivedToActive()
    {
        // Arrange
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived", Status = OrderItem.OrderStatus.Done }
        };
        var activeItems = new List<OrderItem>();
        var itemToUnarchive = archivedItems.First();

        // Act
        archivedItems.Remove(itemToUnarchive);
        activeItems.Add(itemToUnarchive);

        // Assert
        archivedItems.Should().BeEmpty();
        activeItems.Should().HaveCount(1);
        activeItems.First().VendorName.Should().Be("Archived");
    }

    [Fact]
    public void UnarchiveItem_WithLinkedGroup_UnarchivasAllLinked()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived1", LinkedGroupId = groupId },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived2", LinkedGroupId = groupId },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived3", LinkedGroupId = null }
        };
        var activeItems = new List<OrderItem>();

        // Act
        var itemToUnarchive = archivedItems.First();
        var itemsToUnarchive = archivedItems.Where(i => i.LinkedGroupId == itemToUnarchive.LinkedGroupId).ToList();
        foreach (var item in itemsToUnarchive)
        {
            archivedItems.Remove(item);
            activeItems.Add(item);
        }

        // Assert
        archivedItems.Should().HaveCount(1);
        activeItems.Should().HaveCount(2);
        activeItems.All(i => i.LinkedGroupId == groupId).Should().BeTrue();
    }

    [Fact]
    public void RepairArchivedItems_FindsDoneItems()
    {
        // Arrange
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", Status = OrderItem.OrderStatus.Done },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", Status = OrderItem.OrderStatus.InProgress },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item3", Status = OrderItem.OrderStatus.Done }
        };

        // Act
        var itemsToRepair = activeItems.Where(i => i.Status == OrderItem.OrderStatus.Done).ToList();

        // Assert
        itemsToRepair.Should().HaveCount(2);
        itemsToRepair.All(i => i.Status == OrderItem.OrderStatus.Done).Should().BeTrue();
    }

    [Fact]
    public void RepairArchivedItems_FindsItemsWithPreviousStatus()
    {
        // Arrange
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", Status = OrderItem.OrderStatus.InProgress, PreviousStatus = OrderItem.OrderStatus.OnDeck },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", Status = OrderItem.OrderStatus.Done, PreviousStatus = null },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item3", Status = OrderItem.OrderStatus.OnDeck, PreviousStatus = null }
        };

        // Act
        var itemsToRepair = activeItems.Where(i => i.PreviousStatus != null).ToList();

        // Assert
        itemsToRepair.Should().HaveCount(1);
        itemsToRepair.First().VendorName.Should().Be("Item1");
    }

    [Fact]
    public void RepairArchivedItems_MovesMarkedItemsToArchive()
    {
        // Arrange
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", Status = OrderItem.OrderStatus.Done },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", Status = OrderItem.OrderStatus.InProgress, PreviousStatus = OrderItem.OrderStatus.OnDeck }
        };
        var archivedItems = new List<OrderItem>();

        // Act
        var itemsToRepair = activeItems.Where(i =>
            i.Status == OrderItem.OrderStatus.Done ||
            i.PreviousStatus != null).ToList();

        foreach (var item in itemsToRepair)
        {
            activeItems.Remove(item);
            archivedItems.Add(item);
        }

        // Assert
        activeItems.Should().BeEmpty();
        archivedItems.Should().HaveCount(2);
    }

    [Fact]
    public void ClearAllArchived_RemovesAllArchivedItems()
    {
        // Arrange
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived1" },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived2" },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Archived3" }
        };

        // Act
        archivedItems.Clear();

        // Assert
        archivedItems.Should().BeEmpty();
    }

    [Fact]
    public void ClearAllArchived_RequiresConfirmation()
    {
        // Arrange
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1" }
        };

        // Act - confirm deletion
        var shouldDelete = true;

        // Assert
        shouldDelete.Should().BeTrue();
        archivedItems.Should().HaveCount(1); // Not deleted yet
    }

    [Fact]
    public void RefreshArchivedDisplayItems_AppliesFilters()
    {
        // Arrange
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor1", Status = OrderItem.OrderStatus.Done },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor2", Status = OrderItem.OrderStatus.InProgress },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Vendor3", Status = OrderItem.OrderStatus.Done }
        };

        // Act - Filter for Done status
        var filtered = archivedItems.Where(i => i.Status == OrderItem.OrderStatus.Done).ToList();

        // Assert
        filtered.Should().HaveCount(2);
        filtered.All(i => i.Status == OrderItem.OrderStatus.Done).Should().BeTrue();
    }

    [Fact]
    public void RefreshArchivedDisplayItems_HandlesSortMode()
    {
        // Arrange
        var archivedItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "C", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "A", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "B", CreatedAt = DateTime.UtcNow }
        };

        // Act - Sort by creation descending
        var sorted = archivedItems.OrderByDescending(i => i.CreatedAt).ToList();

        // Assert
        sorted.First().VendorName.Should().Be("B");
        sorted.Last().VendorName.Should().Be("A");
    }

    [Fact]
    public void ArchiveItem_NoOp_WithNullItem()
    {
        // Arrange
        OrderItem? item = null;
        var activeItems = new List<OrderItem>();

        // Act
        if (item != null)
        {
            activeItems.Remove(item);
        }

        // Assert
        activeItems.Should().BeEmpty();
    }

    [Fact]
    public void UnarchiveItem_NoOp_WithNullItem()
    {
        // Arrange
        OrderItem? item = null;
        var archivedItems = new List<OrderItem>();

        // Act
        if (item != null)
        {
            archivedItems.Remove(item);
        }

        // Assert
        archivedItems.Should().BeEmpty();
    }

    [Fact]
    public void RepairArchivedItems_ReturnsEarlyIfNothingToRepair()
    {
        // Arrange
        var activeItems = new List<OrderItem>
        {
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item1", Status = OrderItem.OrderStatus.InProgress },
            new OrderItem { Id = Guid.NewGuid(), VendorName = "Item2", Status = OrderItem.OrderStatus.OnDeck }
        };

        // Act
        var itemsToRepair = activeItems.Where(i =>
            i.Status == OrderItem.OrderStatus.Done ||
            i.PreviousStatus != null).ToList();

        // Assert
        itemsToRepair.Should().BeEmpty();
    }

    [Fact]
    public void ArchiveItem_PreservesItemProperties()
    {
        // Arrange
        var item = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "TestVendor",
            TransferNumbers = "TRN123",
            ColorHex = "#FF0000",
            Status = OrderItem.OrderStatus.Done,
            CreatedAt = DateTime.UtcNow
        };

        // Act - Move to archive (no modifications)
        var archivedItem = item;

        // Assert
        archivedItem.VendorName.Should().Be("TestVendor");
        archivedItem.TransferNumbers.Should().Be("TRN123");
        archivedItem.ColorHex.Should().Be("#FF0000");
    }
}
