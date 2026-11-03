using System.Numerics;

namespace Clippings.Core.Editing;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public static PixelRect Empty => new(0, 0, 0, 0);

    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PixelRect FromPoints(Vector2 start, Vector2 end)
    {
        var left = (int)Math.Floor(Math.Min(start.X, end.X));
        var top = (int)Math.Floor(Math.Min(start.Y, end.Y));
        var right = (int)Math.Ceiling(Math.Max(start.X, end.X));
        var bottom = (int)Math.Ceiling(Math.Max(start.Y, end.Y));
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public static PixelRect FromBounds(int x, int y, int width, int height)
        => new(x, y, Math.Max(0, width), Math.Max(0, height));

    public PixelRect ClampTo(int width, int height)
    {
        var left = Math.Clamp(X, 0, Math.Max(0, width));
        var top = Math.Clamp(Y, 0, Math.Max(0, height));
        var right = Math.Clamp(Right, 0, Math.Max(0, width));
        var bottom = Math.Clamp(Bottom, 0, Math.Max(0, height));
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public PixelRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);

    public PixelRect MoveWithin(int dx, int dy, int boundsWidth, int boundsHeight)
    {
        if (IsEmpty)
        {
            return Empty;
        }

        var maxX = Math.Max(0, boundsWidth - Width);
        var maxY = Math.Max(0, boundsHeight - Height);
        return new PixelRect(
            Math.Clamp(X + dx, 0, maxX),
            Math.Clamp(Y + dy, 0, maxY),
            Width,
            Height);
    }

    public PixelRect ResizeWithin(
        ResizeEdges edges,
        int dx,
        int dy,
        int boundsWidth,
        int boundsHeight,
        int minimumWidth = 1,
        int minimumHeight = 1)
    {
        if (IsEmpty || edges == ResizeEdges.None)
        {
            return this;
        }

        var left = X;
        var top = Y;
        var right = Right;
        var bottom = Bottom;
        var minWidth = Math.Max(1, minimumWidth);
        var minHeight = Math.Max(1, minimumHeight);

        if ((edges & ResizeEdges.Left) != 0)
        {
            left = Math.Clamp(X + dx, 0, Math.Max(0, right - minWidth));
        }

        if ((edges & ResizeEdges.Right) != 0)
        {
            right = Math.Clamp(Right + dx, Math.Min(left + minWidth, boundsWidth), Math.Max(0, boundsWidth));
        }

        if ((edges & ResizeEdges.Top) != 0)
        {
            top = Math.Clamp(Y + dy, 0, Math.Max(0, bottom - minHeight));
        }

        if ((edges & ResizeEdges.Bottom) != 0)
        {
            bottom = Math.Clamp(Bottom + dy, Math.Min(top + minHeight, boundsHeight), Math.Max(0, boundsHeight));
        }

        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
