using System.Numerics;
using Clippings.Core.Editing;
using Clippings.Core.Models;

namespace Clippings.Core.Tests;

public class LaserTrailTests
{
    [Fact]
    public void Prune_ComputesProgressFromAge()
    {
        var trail = new LaserTrail(800f);
        trail.Add(new Vector2(0, 0), 0);
        trail.Add(new Vector2(10, 0), 400);

        var points = trail.Prune(400);

        Assert.Equal(2, points.Count);
        Assert.Equal(0.5f, points[0].Progress, 3);
        Assert.Equal(0f, points[1].Progress, 3);
    }

    [Fact]
    public void Prune_RemovesExpiredPoints()
    {
        var trail = new LaserTrail(800f);
        trail.Add(new Vector2(0, 0), 0);
        trail.Add(new Vector2(10, 0), 700);

        var points = trail.Prune(850);

        Assert.Single(points);
        Assert.Equal(new Vector2(10, 0), points[0].Position);
    }

    [Fact]
    public void Prune_EmptyWhenAllExpired()
    {
        var trail = new LaserTrail(500f);
        trail.Add(new Vector2(5, 5), 0);

        Assert.Empty(trail.Prune(600));
        Assert.Equal(0, trail.Count);
    }

    [Fact]
    public void Add_SkipsNearDuplicateWithin16Milliseconds()
    {
        var trail = new LaserTrail(800f);
        trail.Add(new Vector2(3, 3), 100);
        trail.Add(new Vector2(3.1f, 3), 108);

        Assert.Equal(1, trail.Count);
    }

    [Fact]
    public void Clear_RemovesAllPoints()
    {
        var trail = new LaserTrail();
        trail.Add(new Vector2(1, 1), 0);
        trail.Add(new Vector2(2, 2), 50);

        trail.Clear();

        Assert.Equal(0, trail.Count);
        Assert.Empty(trail.Prune(100));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveLifetime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LaserTrail(0f));
    }
}
