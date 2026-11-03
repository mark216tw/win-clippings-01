using System.Numerics;
using Clippings.Core.Models;

namespace Clippings.Core.Editing;

public static class StrokeHitTester
{
    public static bool HitTest(Annotation annotation, Vector2 point, float tolerance)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (annotation.Points.Count == 0)
        {
            return false;
        }

        var radius = Math.Max(1f, tolerance) + annotation.StrokeWidth / 2f;
        switch (annotation.Tool)
        {
            case AnnotationTool.Pen:
            case AnnotationTool.Highlighter:
                return HitTestPolyline(annotation.Points, point, radius);
            case AnnotationTool.Line:
            case AnnotationTool.Arrow:
                return DistanceToSegment(annotation.Points[0], annotation.Points[^1], point) <= radius;
            case AnnotationTool.Rectangle:
                return HitTestRectangle(annotation.Points, point, radius);
            case AnnotationTool.Ellipse:
                return HitTestEllipse(annotation.Points, point, radius);
            case AnnotationTool.Number:
                return Vector2.Distance(annotation.Points[0], point) <= radius + Math.Max(11f, annotation.FontSize * 0.8f);
            default:
                return HitTestBounds(annotation, point, radius);
        }
    }

    private static bool HitTestPolyline(IReadOnlyList<Vector2> points, Vector2 point, float radius)
    {
        if (points.Count == 1)
        {
            return Vector2.Distance(points[0], point) <= radius;
        }

        for (var i = 1; i < points.Count; i++)
        {
            if (DistanceToSegment(points[i - 1], points[i], point) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HitTestRectangle(IReadOnlyList<Vector2> points, Vector2 point, float radius)
    {
        var (left, top, right, bottom) = GetCorners(points);
        if (right - left <= 1f || bottom - top <= 1f)
        {
            return DistanceToSegment(points[0], points[^1], point) <= radius;
        }

        Span<Vector2> corners = stackalloc Vector2[5]
        {
            new(left, top),
            new(right, top),
            new(right, bottom),
            new(left, bottom),
            new(left, top),
        };
        for (var i = 1; i < corners.Length; i++)
        {
            if (DistanceToSegment(corners[i - 1], corners[i], point) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HitTestEllipse(IReadOnlyList<Vector2> points, Vector2 point, float radius)
    {
        var (left, top, right, bottom) = GetCorners(points);
        var a = (right - left) / 2f;
        var b = (bottom - top) / 2f;
        if (a <= 1f || b <= 1f)
        {
            return DistanceToSegment(points[0], points[^1], point) <= radius;
        }

        var center = new Vector2(left + a, top + b);
        var normalized = new Vector2((point.X - center.X) / a, (point.Y - center.Y) / b);
        var distanceToOutline = Math.Abs(normalized.Length() - 1f) * Math.Min(a, b);
        return distanceToOutline <= radius;
    }

    private static bool HitTestBounds(Annotation annotation, Vector2 point, float radius)
    {
        if (annotation.Points.Count == 1)
        {
            var half = Math.Max(10f, annotation.FontSize * 0.6f);
            return Vector2.Distance(annotation.Points[0], point) <= radius + half;
        }

        var (left, top, right, bottom) = GetCorners(annotation.Points);
        return point.X >= left - radius && point.X <= right + radius &&
               point.Y >= top - radius && point.Y <= bottom + radius;
    }

    private static (float Left, float Top, float Right, float Bottom) GetCorners(IReadOnlyList<Vector2> points)
    {
        var start = points[0];
        var end = points.Count > 1 ? points[^1] : start;
        return (
            MathF.Min(start.X, end.X),
            MathF.Min(start.Y, end.Y),
            MathF.Max(start.X, end.X),
            MathF.Max(start.Y, end.Y));
    }

    private static float DistanceToSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector2.Distance(start, point);
        }

        var t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        var projection = start + (segment * t);
        return Vector2.Distance(projection, point);
    }
}
