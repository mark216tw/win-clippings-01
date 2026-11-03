using System.Numerics;

namespace Clippings.Core.Editing;

public readonly record struct LaserTrailPoint(Vector2 Position, float Progress);

public sealed class LaserTrail
{
    private readonly List<Entry> _entries = [];

    public LaserTrail(float lifetimeMilliseconds = 800f)
    {
        if (lifetimeMilliseconds <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetimeMilliseconds));
        }

        LifetimeMilliseconds = lifetimeMilliseconds;
    }

    public float LifetimeMilliseconds { get; }

    public int Count => _entries.Count;

    public void Add(Vector2 position, double timestampMilliseconds)
    {
        if (_entries.Count > 0)
        {
            var last = _entries[^1];
            if (timestampMilliseconds - last.Timestamp < 16 &&
                Vector2.DistanceSquared(last.Position, position) < 0.25f)
            {
                return;
            }
        }

        _entries.Add(new Entry(position, timestampMilliseconds));
    }

    public void Clear() => _entries.Clear();

    public IReadOnlyList<LaserTrailPoint> Prune(double nowMilliseconds)
    {
        var cutoff = nowMilliseconds - LifetimeMilliseconds;
        var expired = 0;
        while (expired < _entries.Count && _entries[expired].Timestamp <= cutoff)
        {
            expired++;
        }

        if (expired > 0)
        {
            _entries.RemoveRange(0, expired);
        }

        var result = new List<LaserTrailPoint>(_entries.Count);
        foreach (var entry in _entries)
        {
            var age = nowMilliseconds - entry.Timestamp;
            var progress = (float)Math.Clamp(age / LifetimeMilliseconds, 0.0, 1.0);
            result.Add(new LaserTrailPoint(entry.Position, progress));
        }

        return result;
    }

    private readonly record struct Entry(Vector2 Position, double Timestamp);
}
