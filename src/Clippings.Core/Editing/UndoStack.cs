namespace Clippings.Core.Editing;

public sealed class UndoStack<T>
{
    private readonly List<T> _undo = [];
    private readonly Stack<T> _redo = new();
    private readonly Action<T>? _discard;

    public UndoStack(int capacity = 50, Action<T>? discard = null)
    {
        Capacity = Math.Max(1, capacity);
        _discard = discard;
    }

    public int Capacity { get; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Push(T previous)
    {
        _undo.Add(previous);
        if (_undo.Count > Capacity)
        {
            _discard?.Invoke(_undo[0]);
            _undo.RemoveAt(0);
        }

        DiscardRedo();
    }

    public T Undo(T current)
    {
        if (_undo.Count == 0)
        {
            throw new InvalidOperationException("沒有可復原的步驟。");
        }

        var previous = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Push(current);
        return previous;
    }

    public T Redo(T current)
    {
        if (_redo.Count == 0)
        {
            throw new InvalidOperationException("沒有可重做的步驟。");
        }

        _undo.Add(current);
        return _redo.Pop();
    }

    public void Clear()
    {
        foreach (var item in _undo)
        {
            _discard?.Invoke(item);
        }

        _undo.Clear();
        DiscardRedo();
    }

    private void DiscardRedo()
    {
        foreach (var item in _redo)
        {
            _discard?.Invoke(item);
        }

        _redo.Clear();
    }
}
