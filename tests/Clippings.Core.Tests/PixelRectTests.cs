using System.Numerics;
using Clippings.Core.Editing;

namespace Clippings.Core.Tests;

public class PixelRectTests
{
    [Fact]
    public void FromPoints_NormalizesReverseDrag()
    {
        var rect = PixelRect.FromPoints(new Vector2(120.4f, 80.2f), new Vector2(20.1f, 30.7f));

        Assert.Equal(20, rect.X);
        Assert.Equal(30, rect.Y);
        Assert.Equal(101, rect.Width);
        Assert.Equal(51, rect.Height);
    }

    [Fact]
    public void ClampTo_KeepsRectInsideBounds()
    {
        var rect = new PixelRect(-10, -5, 300, 200).ClampTo(200, 100);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(200, rect.Width);
        Assert.Equal(100, rect.Height);
    }

    [Fact]
    public void Intersect_ReturnsOverlap()
    {
        var rect = new PixelRect(0, 0, 100, 100).Intersect(new PixelRect(50, 50, 100, 100));

        Assert.Equal(new PixelRect(50, 50, 50, 50), rect);
    }

    [Theory]
    [InlineData(-200, -100, 400, 300, -100, -50, 300, 200, -100, -50, 300, 200)]
    [InlineData(-150, 10, 100, 80, -100, 0, 500, 300, -100, 10, 50, 80)]
    [InlineData(350, 250, 100, 100, -100, 0, 500, 300, 350, 250, 50, 50)]
    [InlineData(600, 400, 100, 100, -100, 0, 500, 300, 600, 400, 0, 0)]
    public void Intersect_ClipsWindowBoundsToVirtualDesktop(
        int x,
        int y,
        int width,
        int height,
        int desktopX,
        int desktopY,
        int desktopWidth,
        int desktopHeight,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var window = new PixelRect(x, y, width, height);
        var desktop = new PixelRect(desktopX, desktopY, desktopWidth, desktopHeight);

        Assert.Equal(
            new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight),
            window.Intersect(desktop));
    }

    [Fact]
    public void Empty_HasZeroSize()
    {
        Assert.True(PixelRect.Empty.IsEmpty);
        Assert.False(new PixelRect(0, 0, 1, 1).IsEmpty);
    }

    [Fact]
    public void MoveWithin_KeepsSizeAndStopsAtBounds()
    {
        var rect = new PixelRect(10, 10, 50, 40);

        Assert.Equal(new PixelRect(30, 25, 50, 40), rect.MoveWithin(20, 15, 200, 200));
        Assert.Equal(new PixelRect(0, 0, 50, 40), rect.MoveWithin(-100, -100, 200, 200));
        Assert.Equal(new PixelRect(150, 160, 50, 40), rect.MoveWithin(500, 500, 200, 200));
    }

    [Theory]
    [InlineData(ResizeEdges.Left, -10, 0, 0, 10, 60, 40)]
    [InlineData(ResizeEdges.Right, 10, 0, 10, 10, 60, 40)]
    [InlineData(ResizeEdges.Top, 0, -10, 10, 0, 50, 50)]
    [InlineData(ResizeEdges.Bottom, 0, 10, 10, 10, 50, 50)]
    [InlineData(ResizeEdges.Left | ResizeEdges.Top, -10, -10, 0, 0, 60, 50)]
    [InlineData(ResizeEdges.Right | ResizeEdges.Bottom, 10, 10, 10, 10, 60, 50)]
    [InlineData(ResizeEdges.Left | ResizeEdges.Bottom, -10, 10, 0, 10, 60, 50)]
    [InlineData(ResizeEdges.Right | ResizeEdges.Top, 10, -10, 10, 0, 60, 50)]
    public void ResizeWithin_ChangesOnlyRequestedEdges(
        ResizeEdges edges,
        int dx,
        int dy,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight)
    {
        var rect = new PixelRect(10, 10, 50, 40);

        var result = rect.ResizeWithin(edges, dx, dy, 200, 200);

        Assert.Equal(expectedX, result.X);
        Assert.Equal(expectedY, result.Y);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
    }

    [Fact]
    public void ResizeWithin_StopsAtMinimumSize()
    {
        var rect = new PixelRect(10, 10, 50, 40);

        var shrunk = rect.ResizeWithin(ResizeEdges.Left, 500, 0, 200, 200, 4, 4);
        var grown = rect.ResizeWithin(ResizeEdges.Right, -500, 0, 200, 200, 4, 4);

        Assert.Equal(4, shrunk.Width);
        Assert.Equal(56, shrunk.X);
        Assert.Equal(4, grown.Width);
        Assert.Equal(10, grown.X);
    }

    [Fact]
    public void ResizeWithin_ClampsToBounds()
    {
        var rect = new PixelRect(10, 10, 50, 40);

        var result = rect.ResizeWithin(
            ResizeEdges.Left | ResizeEdges.Top,
            -500,
            -500,
            200,
            200);

        Assert.Equal(0, result.X);
        Assert.Equal(0, result.Y);
        Assert.Equal(60, result.Width);
        Assert.Equal(50, result.Height);
    }
}
