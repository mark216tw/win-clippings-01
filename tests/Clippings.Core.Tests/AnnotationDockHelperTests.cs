using Clippings.Core.Editing;
using Clippings.Core.Helpers;
using Clippings.Core.Models;

namespace Clippings.Core.Tests;

public class AnnotationDockHelperTests
{
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);

    [Theory]
    [InlineData(100, 12)]
    [InlineData(4, 4)]
    [InlineData(1720, 16)]
    public void DetectEdge_SnapsWithinThreshold(int x, int y)
    {
        var window = new PixelRect(x, y, 200, 80);

        Assert.Equal(AnnotationDockEdge.Top, AnnotationDockHelper.DetectEdge(window, WorkArea, 16));
    }

    [Fact]
    public void DetectEdge_ReturnsNoneOutsideThreshold()
    {
        var window = new PixelRect(400, 300, 200, 80);

        Assert.Equal(AnnotationDockEdge.None, AnnotationDockHelper.DetectEdge(window, WorkArea, 16));
    }

    [Theory]
    [InlineData(0, 200)]
    [InlineData(1720, 200)]
    public void DetectEdge_IgnoresLeftAndRightEdges(int x, int y)
    {
        var window = new PixelRect(x, y, 200, 80);

        Assert.Equal(AnnotationDockEdge.None, AnnotationDockHelper.DetectEdge(window, WorkArea, 16));
    }

    [Fact]
    public void DetectEdge_HandlesNegativeMonitorOrigin()
    {
        var work = new PixelRect(-1920, -120, 1920, 1080);
        var window = new PixelRect(-1800, -120, 200, 80);

        Assert.Equal(AnnotationDockEdge.Top, AnnotationDockHelper.DetectEdge(window, work, 16));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 860)]
    [InlineData(1.0, 1720)]
    public void ComputeBounds_PlacesWindowOnTopEdge(double ratio, int expectedX)
    {
        var bounds = AnnotationDockHelper.ComputeBounds(
            WorkArea,
            200,
            80,
            AnnotationDockEdge.Top,
            ratio);

        Assert.Equal(expectedX, bounds.X);
        Assert.Equal(WorkArea.Y, bounds.Y);
    }

    [Theory]
    [InlineData(AnnotationDockEdge.Left)]
    [InlineData(AnnotationDockEdge.Right)]
    public void ComputeBounds_ReturnsEmptyForSideEdges(AnnotationDockEdge edge)
    {
        var bounds = AnnotationDockHelper.ComputeBounds(WorkArea, 200, 80, edge, 0.5);

        Assert.True(bounds.IsEmpty);
    }

    [Fact]
    public void ComputeOffsetRatio_RoundTrips()
    {
        var window = new PixelRect(860, 0, 200, 80);

        Assert.Equal(
            0.5,
            AnnotationDockHelper.ComputeOffsetRatio(window, WorkArea, AnnotationDockEdge.Top),
            3);
    }

    [Theory]
    [InlineData(AnnotationDockEdge.Left)]
    [InlineData(AnnotationDockEdge.Right)]
    public void ComputeOffsetRatio_ReturnsCenterForSideEdges(AnnotationDockEdge edge)
    {
        var window = new PixelRect(0, 480, 200, 80);

        Assert.Equal(0.5, AnnotationDockHelper.ComputeOffsetRatio(window, WorkArea, edge));
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(double.NaN, 0.5)]
    public void ComputeBounds_ClampsRatio(double ratio, double expectedRatio)
    {
        var bounds = AnnotationDockHelper.ComputeBounds(WorkArea, 200, 80, AnnotationDockEdge.Top, ratio);
        var expected = AnnotationDockHelper.ComputeBounds(WorkArea, 200, 80, AnnotationDockEdge.Top, expectedRatio);

        Assert.Equal(expected, bounds);
    }

    [Theory]
    [InlineData(AnnotationDockEdge.None, AnnotationDockEdge.None)]
    [InlineData(AnnotationDockEdge.Top, AnnotationDockEdge.Top)]
    [InlineData(AnnotationDockEdge.Left, AnnotationDockEdge.None)]
    [InlineData(AnnotationDockEdge.Right, AnnotationDockEdge.None)]
    public void NormalizeEdge_OnlyKeepsTop(AnnotationDockEdge edge, AnnotationDockEdge expected)
    {
        Assert.Equal(expected, AnnotationDockHelper.NormalizeEdge(edge));
    }

    [Fact]
    public void ComputeBounds_HandlesWindowLargerThanWorkArea()
    {
        var work = new PixelRect(0, 0, 100, 100);

        var bounds = AnnotationDockHelper.ComputeBounds(work, 200, 200, AnnotationDockEdge.Top, 0.5);

        Assert.Equal(new PixelRect(0, 0, 200, 200), bounds);
    }

    [Fact]
    public void ClampToWorkArea_KeepsWindowInside()
    {
        var window = new PixelRect(-50, 1100, 200, 80);

        var clamped = AnnotationDockHelper.ClampToWorkArea(window, WorkArea);

        Assert.Equal(new PixelRect(0, 960, 200, 80), clamped);
    }
}
