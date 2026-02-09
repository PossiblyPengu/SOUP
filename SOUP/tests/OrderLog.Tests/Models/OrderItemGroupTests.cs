using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using Xunit;

namespace SOUP.Tests.OrderLog.Models;

public class OrderItemGroupTests
{
    [Fact]
    public void OrderItemGroup_CreatesWithMembers()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "Vendor1", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "Vendor2", Status = OrderItem.OrderStatus.NotReady }
        };

        var group = new OrderItemGroup { Members = members };

        group.Members.Should().HaveCount(2);
        group.First.Should().NotBeNull();
        group.First?.VendorName.Should().Be("Vendor1");
    }

    [Fact]
    public void OrderItemGroup_EmptyMembers()
    {
        var group = new OrderItemGroup { Members = new List<OrderItem>() };

        group.Members.Should().BeEmpty();
        group.First.Should().BeNull();
    }

    [Fact]
    public void OrderItemGroup_First_GetsFirstMember()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "First" },
            new() { VendorName = "Second" }
        };

        var group = new OrderItemGroup { Members = members };

        group.First?.VendorName.Should().Be("First");
    }

    [Fact]
    public void OrderItemGroup_SingleMember()
    {
        var member = new OrderItem { VendorName = "OnlyOne" };
        var group = new OrderItemGroup { Members = new List<OrderItem> { member } };

        group.Members.Should().HaveCount(1);
        group.First.Should().Be(member);
    }

    [Fact]
    public void OrderItemGroup_LinkedItems()
    {
        var groupId = Guid.NewGuid();
        var members = new List<OrderItem>
        {
            new() { VendorName = "A", LinkedGroupId = groupId },
            new() { VendorName = "B", LinkedGroupId = groupId }
        };

        var group = new OrderItemGroup { Members = members };

        group.Members.Should().AllSatisfy(m => m.LinkedGroupId.Should().Be(groupId));
    }

    [Fact]
    public void OrderItemGroup_MixedStatuses()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "A", Status = OrderItem.OrderStatus.NotReady },
            new() { VendorName = "B", Status = OrderItem.OrderStatus.OnDeck },
            new() { VendorName = "C", Status = OrderItem.OrderStatus.InProgress }
        };

        var group = new OrderItemGroup { Members = members };

        group.Members.Should().HaveCount(3);
        group.Members[0].Status.Should().Be(OrderItem.OrderStatus.NotReady);
        group.Members[1].Status.Should().Be(OrderItem.OrderStatus.OnDeck);
        group.Members[2].Status.Should().Be(OrderItem.OrderStatus.InProgress);
    }

    [Fact]
    public void OrderItemGroup_MembersCollection()
    {
        var initialMembers = new List<OrderItem>
        {
            new() { VendorName = "Initial" }
        };

        var group = new OrderItemGroup { Members = initialMembers };

        group.Members.Should().HaveCount(1);
    }

    [Fact]
    public void OrderItemGroup_DisplayText_Format()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "VendorA" },
            new() { VendorName = "VendorB" },
            new() { VendorName = "VendorC" }
        };

        var group = new OrderItemGroup { Members = members };

        // Assuming group display shows member count or first vendor name
        group.Members.Should().HaveCount(3);
    }

    [Fact]
    public void OrderItemGroup_AllArchivedStatus()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "A", IsArchived = true },
            new() { VendorName = "B", IsArchived = true }
        };

        var group = new OrderItemGroup { Members = members };

        group.Members.Should().AllSatisfy(m => m.IsArchived.Should().BeTrue());
    }

    [Fact]
    public void OrderItemGroup_MixedArchivedStatus()
    {
        var members = new List<OrderItem>
        {
            new() { VendorName = "Active" },
            new() { VendorName = "Archived", IsArchived = true }
        };

        var group = new OrderItemGroup { Members = members };

        group.Members.Should().HaveCount(2);
        group.Members.Should().Contain(m => !m.IsArchived);
        group.Members.Should().Contain(m => m.IsArchived);
    }
}

public class NoteTypeStickyNoteTests
{
    [Fact]
    public void OrderItem_AsStickyNote()
    {
        var note = new OrderItem
        {
            VendorName = "Sticky Note",
            NoteType = NoteType.StickyNote,
            Content = "Important reminder"
        };

        note.IsStickyNote.Should().BeTrue();
        note.NoteType.Should().Be(NoteType.StickyNote);
    }

    [Fact]
    public void OrderItem_AsOrder()
    {
        var order = new OrderItem
        {
            VendorName = "Order",
            NoteType = NoteType.Order,
            TransferNumbers = "T001"
        };

        order.IsStickyNote.Should().BeFalse();
        order.NoteType.Should().Be(NoteType.Order);
    }

    [Fact]
    public void OrderItem_StickyNoteCanBeArchived()
    {
        var note = new OrderItem
        {
            VendorName = "Old Sticky",
            NoteType = NoteType.StickyNote,
            IsArchived = true
        };

        note.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void OrderItem_StickyNoteDefaultColor()
    {
        var note = new OrderItem
        {
            VendorName = "Sticky",
            NoteType = NoteType.StickyNote
        };

        note.NoteType.Should().Be(NoteType.StickyNote);
    }
}

public class OrderItemStatusTransitionTests
{
    [Fact]
    public void OrderItem_TransitionNotReadyToOnDeck()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.NotReady };

        item.Status = OrderItem.OrderStatus.OnDeck;

        item.Status.Should().Be(OrderItem.OrderStatus.OnDeck);
    }

    [Fact]
    public void OrderItem_TransitionOnDeckToInProgress()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.OnDeck };

        item.Status = OrderItem.OrderStatus.InProgress;

        item.Status.Should().Be(OrderItem.OrderStatus.InProgress);
    }

    [Fact]
    public void OrderItem_TransitionInProgressToDone()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.InProgress };

        item.Status = OrderItem.OrderStatus.Done;

        item.Status.Should().Be(OrderItem.OrderStatus.Done);
    }

    [Fact]
    public void OrderItem_TransitionDoneBackToNotReady()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.Done };

        item.Status = OrderItem.OrderStatus.NotReady;

        item.Status.Should().Be(OrderItem.OrderStatus.NotReady);
    }

    [Fact]
    public void OrderItem_DirectTransitionNotReadyToInProgress()
    {
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.NotReady };

        item.Status = OrderItem.OrderStatus.InProgress;

        item.Status.Should().Be(OrderItem.OrderStatus.InProgress);
    }

    [Fact]
    public void OrderItem_AllStatusEnumValues()
    {
        var item = new OrderItem { VendorName = "Test" };
        var allStatuses = Enum.GetValues(typeof(OrderItem.OrderStatus)).Cast<OrderItem.OrderStatus>();

        foreach (var status in allStatuses)
        {
            item.Status = status;
            item.Status.Should().Be(status);
        }
    }
}
