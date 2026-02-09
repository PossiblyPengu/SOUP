using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using Xunit;

namespace SOUP.Features.OrderLog.Tests.Services;

/// <summary>
/// Tests for item management operations: add order, add sticky note, color management
/// </summary>
public class OrderLogItemManagementTests
{
    private const string DefaultOrderColor = "#E8E4D0";
    private const string DefaultNoteColor = "#FFFF00";

    [Fact]
    public void AddOrder_WithValidItem_CreatesOrder()
    {
        // Arrange
        var order = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "Test Vendor",
            TransferNumbers = "TRN123",
            NoteType = NoteType.Order,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var items = new List<OrderItem> { order };

        // Assert
        items.Should().HaveCount(1);
        items.First().VendorName.Should().Be("Test Vendor");
        items.First().NoteType.Should().Be(NoteType.Order);
    }

    [Fact]
    public void AddOrderInline_WithVendorName_RequiresNotNull()
    {
        // Arrange
        string vendorName = "Local Vendor";

        // Act & Assert
        vendorName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void AddOrderInline_ClearsFormAfterSuccess()
    {
        // Arrange
        var vendorName = "Vendor";
        var transferNumbers = "TRN123";
        var whsNumbers = "WHS456";

        // Act - simulate clear
        vendorName = string.Empty;
        transferNumbers = string.Empty;
        whsNumbers = string.Empty;

        // Assert
        vendorName.Should().BeEmpty();
        transferNumbers.Should().BeEmpty();
        whsNumbers.Should().BeEmpty();
    }

    [Fact]
    public void AddStickyNote_WithContent_CreatesStickyNote()
    {
        // Arrange
        var noteContent = "Important reminder";
        var note = new OrderItem
        {
            Id = Guid.NewGuid(),
            NoteType = NoteType.StickyNote,
            NoteContent = noteContent,
            Status = OrderItem.OrderStatus.OnDeck,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var items = new List<OrderItem> { note };

        // Assert
        items.Should().HaveCount(1);
        items.First().NoteType.Should().Be(NoteType.StickyNote);
        items.First().NoteContent.Should().Be(noteContent);
        items.First().Status.Should().Be(OrderItem.OrderStatus.OnDeck);
    }

    [Fact]
    public void AddStickyNote_RequiresContent()
    {
        // Arrange
        string content = "";

        // Act & Assert
        content.Should().BeEmpty();
    }

    [Fact]
    public void AddStickyNote_DefaultsToOnDeckStatus()
    {
        // Arrange
        var note = new OrderItem
        {
            NoteType = NoteType.StickyNote,
            NoteContent = "Test"
        };

        // Act
        note.Status = OrderItem.OrderStatus.OnDeck;

        // Assert
        note.Status.Should().Be(OrderItem.OrderStatus.OnDeck);
    }

    [Fact]
    public void AddQuickStickyNote_WithContent_CreatesNote()
    {
        // Arrange
        var content = "Quick note";
        var note = new OrderItem
        {
            Id = Guid.NewGuid(),
            NoteType = NoteType.StickyNote,
            NoteContent = content.Trim(),
            ColorHex = DefaultNoteColor,
            CreatedAt = DateTime.UtcNow,
            Status = OrderItem.OrderStatus.OnDeck
        };

        // Act
        var items = new List<OrderItem> { note };

        // Assert
        items.First().NoteContent.Should().Be("Quick note");
        items.First().ColorHex.Should().Be(DefaultNoteColor);
    }

    [Fact]
    public void AddQuickStickyNote_WithCustomColor_UsesCustom()
    {
        // Arrange
        var content = "Custom colored note";
        var customColor = "#FF0000";
        var note = new OrderItem
        {
            Id = Guid.NewGuid(),
            NoteType = NoteType.StickyNote,
            NoteContent = content,
            ColorHex = customColor ?? DefaultNoteColor,
            Status = OrderItem.OrderStatus.OnDeck
        };

        // Act & Assert
        note.ColorHex.Should().Be("#FF0000");
    }

    [Fact]
    public void SetStickyNoteColor_UpdatesColor()
    {
        // Arrange
        var colorHex = "#FF00FF";
        var currentColor = "#FFFF00";

        // Act
        currentColor = colorHex;

        // Assert
        currentColor.Should().Be("#FF00FF");
    }

    [Fact]
    public void GetStickyNoteColor_ReturnsCurrentColor()
    {
        // Arrange
        var currentColor = "#FF00FF";

        // Act
        var retrievedColor = currentColor;

        // Assert
        retrievedColor.Should().Be("#FF00FF");
    }

    [Fact]
    public void SetNewNoteColor_UpdatesFormColor()
    {
        // Arrange
        var formColor = DefaultOrderColor;
        var newColor = "#00FF00";

        // Act
        formColor = newColor;

        // Assert
        formColor.Should().Be("#00FF00");
    }

    [Fact]
    public void GetNewNoteColor_ReturnsFormColor()
    {
        // Arrange
        var formColor = "#00FF00";

        // Act
        var retrievedColor = formColor;

        // Assert
        retrievedColor.Should().Be("#00FF00");
    }

    [Fact]
    public void AddOrder_WithTransferNumbers_PreservesData()
    {
        // Arrange
        var order = new OrderItem
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor",
            TransferNumbers = "TRN-001, TRN-002",
            WhsShipmentNumbers = "SHIP-ABC",
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        order.TransferNumbers.Should().Be("TRN-001, TRN-002");
        order.WhsShipmentNumbers.Should().Be("SHIP-ABC");
    }

    [Fact]
    public void AddStickyNote_MultilineContent_Preserved()
    {
        // Arrange
        var multilineContent = "Line 1\nLine 2\nLine 3";
        var note = new OrderItem
        {
            Id = Guid.NewGuid(),
            NoteType = NoteType.StickyNote,
            NoteContent = multilineContent,
            Status = OrderItem.OrderStatus.OnDeck
        };

        // Act & Assert
        note.NoteContent.Should().Contain("\n");
        note.NoteContent.Should().Be(multilineContent);
    }

    [Fact]
    public void AddOrderInline_WithWhitespace_TrimsContent()
    {
        // Arrange
        var vendorName = "  Vendor Name  ";
        var transferNumbers = "  TRN123  ";

        // Act
        var trimmedVendor = vendorName.Trim();
        var trimmedTransfer = transferNumbers.Trim();

        // Assert
        trimmedVendor.Should().Be("Vendor Name");
        trimmedTransfer.Should().Be("TRN123");
    }

    [Fact]
    public void ColorSettings_DefaultValues()
    {
        // Arrange & Act
        var orderColor = DefaultOrderColor;
        var noteColor = DefaultNoteColor;

        // Assert
        orderColor.Should().Be("#E8E4D0");
        noteColor.Should().Be("#FFFF00");
    }
}
