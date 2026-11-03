using Clippings.Core.Editing;

namespace Clippings.Core.Tests;

public class UndoStackTests
{
    [Fact]
    public void Push_ThenUndo_ReturnsPreviousState()
    {
        var stack = new UndoStack<string>();
        stack.Push("a");
        stack.Push("b");

        Assert.True(stack.CanUndo);
        Assert.Equal("b", stack.Undo("c"));
        Assert.Equal("a", stack.Undo("b"));
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void Redo_ReplaysUndoneStates()
    {
        var stack = new UndoStack<int>();
        stack.Push(1);
        var undone = stack.Undo(2);

        Assert.Equal(1, undone);
        Assert.True(stack.CanRedo);
        Assert.Equal(2, stack.Redo(1));
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Push_ClearsRedoHistory()
    {
        var stack = new UndoStack<int>();
        stack.Push(1);
        stack.Undo(2);
        stack.Push(5);

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Push_RespectsCapacity()
    {
        var stack = new UndoStack<int>(capacity: 2);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);

        Assert.Equal(3, stack.Undo(4));
        Assert.Equal(2, stack.Undo(3));
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void Undo_WithoutHistory_Throws()
    {
        var stack = new UndoStack<int>();
        Assert.Throws<InvalidOperationException>(() => stack.Undo(1));
    }

    [Fact]
    public void Push_DiscardsEvictedAndRedoStates()
    {
        var discarded = new List<int>();
        var stack = new UndoStack<int>(capacity: 2, discarded.Add);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Undo(4);
        stack.Push(5);

        Assert.Equal([1, 4], discarded);
    }

    [Fact]
    public void Clear_DiscardsAllStates()
    {
        var discarded = new List<int>();
        var stack = new UndoStack<int>(discard: discarded.Add);
        stack.Push(1);
        stack.Push(2);
        stack.Undo(3);

        stack.Clear();

        Assert.Equal([1, 3], discarded);
    }
}
