using Clippings.Core.Editing;
using Clippings.Core.Models;

namespace Clippings.Core.Helpers;

public static class AnnotationDockHelper
{
    public static AnnotationDockEdge DetectEdge(PixelRect window, PixelRect workArea, int thresholdPixels)
    {
        if (window.IsEmpty || workArea.IsEmpty || thresholdPixels < 0)
        {
            return AnnotationDockEdge.None;
        }

        var top = Math.Abs(window.Y - workArea.Y);
        return top <= thresholdPixels ? AnnotationDockEdge.Top : AnnotationDockEdge.None;
    }

    public static AnnotationDockEdge NormalizeEdge(AnnotationDockEdge edge)
        => edge == AnnotationDockEdge.Top ? AnnotationDockEdge.Top : AnnotationDockEdge.None;

    public static PixelRect ComputeBounds(
        PixelRect workArea,
        int width,
        int height,
        AnnotationDockEdge edge,
        double offsetRatio)
    {
        if (workArea.IsEmpty || width <= 0 || height <= 0 ||
            NormalizeEdge(edge) != AnnotationDockEdge.Top)
        {
            return PixelRect.Empty;
        }

        var ratio = NormalizeRatio(offsetRatio);
        var maxX = Math.Max(0, workArea.Width - width);
        var x = workArea.X + (int)Math.Round(ratio * maxX);
        return new PixelRect(x, workArea.Y, width, height);
    }

    public static double ComputeOffsetRatio(PixelRect window, PixelRect workArea, AnnotationDockEdge edge)
    {
        if (window.IsEmpty || workArea.IsEmpty ||
            NormalizeEdge(edge) != AnnotationDockEdge.Top)
        {
            return 0.5;
        }

        return Ratio(window.X - workArea.X, workArea.Width - window.Width);
    }

    public static PixelRect ClampToWorkArea(PixelRect window, PixelRect workArea)
    {
        if (window.IsEmpty || workArea.IsEmpty)
        {
            return window;
        }

        var maxX = Math.Max(workArea.X, workArea.Right - window.Width);
        var maxY = Math.Max(workArea.Y, workArea.Bottom - window.Height);
        return new PixelRect(
            Math.Clamp(window.X, workArea.X, maxX),
            Math.Clamp(window.Y, workArea.Y, maxY),
            window.Width,
            window.Height);
    }

    public static double NormalizeRatio(double ratio)
        => double.IsFinite(ratio) ? Math.Clamp(ratio, 0, 1) : 0.5;

    private static double Ratio(int offset, int travel)
        => travel <= 0 ? 0.5 : NormalizeRatio((double)offset / travel);
}
