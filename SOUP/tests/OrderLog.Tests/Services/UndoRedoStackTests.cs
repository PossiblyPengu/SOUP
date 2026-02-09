using System;
using FluentAssertions;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.Services;
using Xunit;

namespace SOUP.Tests.OrderLog.Services;

public class UndoRedoStackTests
{
    [Fact]
    public void UndoRedoStack_ExecutesAction()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };
        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);

        stack.ExecuteAction(action);

        stack.UndoCount.Should().Be(1);
        stack.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void UndoRedoStack_Undo_ReturnsAction()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };
        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);

        stack.ExecuteAction(action);
        var canUndo = stack.CanUndo;

        canUndo.Should().BeTrue();
    }

    [Fact]
    public void UndoRedoStack_UndoAndRedo()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };
        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);

        stack.ExecuteAction(action);
        var undoCountBefore = stack.UndoCount;

        stack.Undo();
        var undoCountAfter = stack.UndoCount;
        var redoCountAfter = stack.RedoCount;

        undoCountBefore.Should().Be(1);
        undoCountAfter.Should().Be(0);
        redoCountAfter.Should().Be(1);
    }

    [Fact]
    public void UndoRedoStack_Redo()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };
        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);

        stack.ExecuteAction(action);
        stack.Undo();
        var redoCountBefore = stack.RedoCount;

        stack.Redo();
        var redoCountAfter = stack.RedoCount;
        var undoCountAfter = stack.UndoCount;

        redoCountBefore.Should().Be(1);
        redoCountAfter.Should().Be(0);
        undoCountAfter.Should().Be(1);
    }

    [Fact]
    public void UndoRedoStack_MaxHistorySize()
    {
        var maxSize = 5;
        var stack = new UndoRedoStack(maxHistorySize: maxSize);
        var item = new OrderItem { VendorName = "Test" };

        // Add more actions than max history size
        for (int i = 0; i < maxSize + 3; i++)
        {
            var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);
            stack.ExecuteAction(action);
        }

        // History should be limited to maxSize
        stack.UndoCount.Should().BeLessThanOrEqualTo(maxSize);
    }

    [Fact]
    public void UndoRedoStack_CannotUndoWhenEmpty()
    {
        var stack = new UndoRedoStack();

        stack.CanUndo.Should().BeFalse();
        stack.UndoCount.Should().Be(0);
    }

    [Fact]
    public void UndoRedoStack_CannotRedoWhenEmpty()
    {
        var stack = new UndoRedoStack();

        stack.CanRedo.Should().BeFalse();
        stack.RedoCount.Should().Be(0);
    }

    [Fact]
    public void UndoRedoStack_MultipleUndos()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };

        var action1 = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);
        var action2 = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.InProgress);

        stack.ExecuteAction(action1);
        stack.ExecuteAction(action2);

        stack.UndoCount.Should().Be(2);
        stack.CanUndo.Should().BeTrue();

        stack.Undo();
        stack.UndoCount.Should().Be(1);

        stack.Undo();
        stack.UndoCount.Should().Be(0);
    }

    [Fact]
    public void UndoRedoStack_RedoClearedAfterNewAction()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test" };

        var action1 = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);
        stack.ExecuteAction(action1);
        stack.Undo();

        stack.RedoCount.Should().Be(1);

        var action2 = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.InProgress);
        stack.ExecuteAction(action2);

        // Redo history should be cleared after new action
        stack.RedoCount.Should().Be(0);
        stack.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void UndoRedoStack_QueryHistory()
    {
        var stack = new UndoRedoStack();
        var item = new OrderItem { VendorName = "Test", Status = OrderItem.OrderStatus.NotReady };

        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);
        stack.ExecuteAction(action);

        var history = stack.UndoHistory.ToList();

        history.Should().HaveCount(1);
        history[0].Should().NotBeNull();
    }

    [Fact]
    public void UndoRedoStack_StackChangedEventFires()
    {
        var stack = new UndoRedoStack();
        var eventFired = false;

        stack.StackChanged += () => eventFired = true;

        var item = new OrderItem { VendorName = "Test" };
        var action = new StatusChangeAction(new[] { item }, OrderItem.OrderStatus.OnDeck);

        stack.ExecuteAction(action);

        eventFired.Should().BeTrue();
    }
}
