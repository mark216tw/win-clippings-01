using System.Numerics;
using Clippings.Core.Editing;
using Clippings.Core.Models;

namespace Clippings.App.Services;

public sealed class AnnotationDocument
{
    private readonly UndoStack<Dictionary<int, List<Annotation>>> _undo = new(50);
    private Dictionary<int, List<Annotation>> _strokes = [];
    private bool _lastOpWasErase;

    public event EventHandler? Changed;

    public bool CanUndo => _undo.CanUndo;

    public bool CanRedo => _undo.CanRedo;

    public bool HasStrokes => _strokes.Values.Any(list => list.Count > 0);

    public IReadOnlyList<Annotation> GetStrokes(int monitorIndex)
        => _strokes.TryGetValue(monitorIndex, out var list) ? list : [];

    public void Add(int monitorIndex, Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        PushSnapshot();
        _lastOpWasErase = false;
        if (!_strokes.TryGetValue(monitorIndex, out var list))
        {
            list = [];
            _strokes[monitorIndex] = list;
        }

        list.Add(annotation);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool EraseAt(int monitorIndex, Vector2 point, float tolerance)
    {
        if (!_strokes.TryGetValue(monitorIndex, out var list) || list.Count == 0)
        {
            return false;
        }

        var hits = list.Where(stroke => StrokeHitTester.HitTest(stroke, point, tolerance)).ToList();
        if (hits.Count == 0)
        {
            return false;
        }

        if (!_lastOpWasErase)
        {
            PushSnapshot();
            _lastOpWasErase = true;
        }

        foreach (var stroke in hits)
        {
            list.Remove(stroke);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ClearAll()
    {
        if (!HasStrokes)
        {
            return false;
        }

        PushSnapshot();
        _lastOpWasErase = false;
        _strokes = [];
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Undo()
    {
        if (!_undo.CanUndo)
        {
            return;
        }

        _lastOpWasErase = false;
        _strokes = _undo.Undo(CloneStrokes(_strokes));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!_undo.CanRedo)
        {
            return;
        }

        _lastOpWasErase = false;
        _strokes = _undo.Redo(CloneStrokes(_strokes));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void PushSnapshot() => _undo.Push(CloneStrokes(_strokes));

    private static Dictionary<int, List<Annotation>> CloneStrokes(Dictionary<int, List<Annotation>> source)
        => source.ToDictionary(pair => pair.Key, pair => new List<Annotation>(pair.Value));
}
