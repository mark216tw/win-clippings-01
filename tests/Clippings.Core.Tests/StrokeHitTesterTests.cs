using System.Numerics;
using Clippings.Core.Editing;
using Clippings.Core.Models;

namespace Clippings.Core.Tests;

public class StrokeHitTesterTests
{
    private static Annotation Make(AnnotationTool tool, params Vector2[] points) => new()
    {
        Tool = tool,
        Points = points,
        StrokeWidth = 4f,
        FontSize = 24f,
    };

    [Fact]
    public void Pen_HitsNearSegment()
    {
        var stroke = Make(AnnotationTool.Pen, new Vector2(0, 0), new Vector2(100, 0));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(50, 3), 8f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(50, 40), 8f));
    }

    [Fact]
    public void Highlighter_HitsNearSegment()
    {
        var stroke = Make(AnnotationTool.Highlighter, new Vector2(0, 10), new Vector2(80, 10));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(20, 16), 8f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(20, 60), 8f));
    }

    [Fact]
    public void Line_HitsOnlyNearTheLine()
    {
        var stroke = Make(AnnotationTool.Line, new Vector2(0, 0), new Vector2(100, 100));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(50, 54), 8f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(10, 90), 8f));
    }

    [Fact]
    public void Rectangle_HitsEdgeButNotCenter()
    {
        var stroke = Make(AnnotationTool.Rectangle, new Vector2(0, 0), new Vector2(100, 60));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(50, 0), 8f));
        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(100, 30), 8f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(50, 30), 8f));
    }

    [Fact]
    public void Ellipse_HitsOutlineButNotCenter()
    {
        var stroke = Make(AnnotationTool.Ellipse, new Vector2(0, 0), new Vector2(100, 60));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(50, 0), 8f));
        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(0, 30), 8f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(50, 30), 8f));
    }

    [Fact]
    public void Number_HitsAroundMarker()
    {
        var stroke = new Annotation
        {
            Tool = AnnotationTool.Number,
            Points = [new Vector2(40, 40)],
            StrokeWidth = 4f,
            FontSize = 24f,
            Number = 1,
        };

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(48, 40), 4f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(80, 40), 4f));
    }

    [Fact]
    public void EmptyAnnotation_IsNeverHit()
    {
        var stroke = Make(AnnotationTool.Pen);

        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(0, 0), 100f));
    }

    [Fact]
    public void SinglePointStroke_HitsAroundPoint()
    {
        var stroke = Make(AnnotationTool.Pen, new Vector2(10, 10));

        Assert.True(StrokeHitTester.HitTest(stroke, new Vector2(13, 10), 4f));
        Assert.False(StrokeHitTester.HitTest(stroke, new Vector2(30, 10), 4f));
    }
}
