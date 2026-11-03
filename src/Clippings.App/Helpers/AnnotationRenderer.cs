using System.Globalization;
using System.Numerics;
using Clippings.Core.Editing;
using Clippings.Core.Models;
using SkiaSharp;

namespace Clippings.App.Helpers;

public static class AnnotationRenderer
{
    public const byte HighlighterAlpha = 77;

    public static void Draw(
        SKCanvas canvas,
        Annotation annotation,
        SKTypeface typeface,
        Func<Annotation, SKRect, SKBitmap?>? mosaicResolver = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(typeface);

        if (annotation.Points.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = annotation.StrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = ToSKColor(annotation.ColorArgb),
        };

        switch (annotation.Tool)
        {
            case AnnotationTool.Arrow:
                DrawArrow(canvas, annotation, paint);
                break;
            case AnnotationTool.Line:
                canvas.DrawLine(ToPoint(annotation.Points[0]), ToPoint(annotation.Points[^1]), paint);
                break;
            case AnnotationTool.Rectangle:
                canvas.DrawRect(GetRect(annotation), paint);
                break;
            case AnnotationTool.Ellipse:
                canvas.DrawOval(GetRect(annotation), paint);
                break;
            case AnnotationTool.Pen:
                DrawPen(canvas, annotation, paint);
                break;
            case AnnotationTool.Highlighter:
                DrawHighlighter(canvas, annotation);
                break;
            case AnnotationTool.Text:
                DrawTextAnnotation(canvas, annotation, typeface);
                break;
            case AnnotationTool.Mosaic:
                DrawMosaic(canvas, annotation, mosaicResolver);
                break;
            case AnnotationTool.Number:
                DrawNumber(canvas, annotation, typeface);
                break;
        }
    }

    public static void DrawLaserTrail(
        SKCanvas canvas,
        IReadOnlyList<LaserTrailPoint> trail,
        SKColor color,
        float strokeWidth)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(trail);

        if (trail.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        for (var i = 1; i < trail.Count; i++)
        {
            var alpha = (byte)Math.Clamp((int)(255f * (1f - trail[i].Progress)), 0, 255);
            if (alpha == 0)
            {
                continue;
            }

            paint.Color = color.WithAlpha(alpha);
            canvas.DrawLine(ToPoint(trail[i - 1].Position), ToPoint(trail[i].Position), paint);
        }

        using var headPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color,
        };
        canvas.DrawCircle(ToPoint(trail[^1].Position), MathF.Max(2f, strokeWidth / 2f), headPaint);
    }

    public static SKRect GetRect(Annotation annotation)
    {
        var start = annotation.Points[0];
        var end = annotation.Points.Count > 1 ? annotation.Points[^1] : start;
        var left = MathF.Min(start.X, end.X);
        var top = MathF.Min(start.Y, end.Y);
        return SKRect.Create(left, top, MathF.Abs(end.X - start.X), MathF.Abs(end.Y - start.Y));
    }

    public static SKColor ToSKColor(uint argb) => new(
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));

    public static uint ToArgb(SKColor color)
        => ((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;

    public static uint ToAnnotationArgb(AnnotationTool tool, SKColor color)
        => ToArgb(tool == AnnotationTool.Highlighter ? color.WithAlpha(HighlighterAlpha) : color);

    private static SKPoint ToPoint(Vector2 point) => new(point.X, point.Y);

    private static void DrawArrow(SKCanvas canvas, Annotation annotation, SKPaint paint)
    {
        var start = annotation.Points[0];
        var end = annotation.Points[^1];
        canvas.DrawLine(ToPoint(start), ToPoint(end), paint);

        var angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
        var head = Math.Max(9f, annotation.StrokeWidth * 3.2f);
        foreach (var delta in new[] { -0.5f, 0.5f })
        {
            var tip = new SKPoint(
                end.X - head * MathF.Cos(angle + delta),
                end.Y - head * MathF.Sin(angle + delta));
            canvas.DrawLine(ToPoint(end), tip, paint);
        }
    }

    private static void DrawPen(SKCanvas canvas, Annotation annotation, SKPaint paint)
    {
        if (annotation.Points.Count < 2)
        {
            return;
        }

        var points = new SKPoint[annotation.Points.Count];
        for (var i = 0; i < annotation.Points.Count; i++)
        {
            points[i] = ToPoint(annotation.Points[i]);
        }

        canvas.DrawPoints(SKPointMode.Polygon, points, paint);
    }

    private static void DrawHighlighter(SKCanvas canvas, Annotation annotation)
    {
        if (annotation.Points.Count < 2)
        {
            return;
        }

        var color = ToSKColor(annotation.ColorArgb);
        if (color.Alpha == 0)
        {
            return;
        }

        using var builder = new SKPathBuilder();
        builder.MoveTo(annotation.Points[0].X, annotation.Points[0].Y);
        for (var i = 1; i < annotation.Points.Count; i++)
        {
            builder.LineTo(annotation.Points[i].X, annotation.Points[i].Y);
        }

        using var path = builder.Detach();

        var strokeWidth = annotation.StrokeWidth * 4.5f;
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(color.Red, color.Green, color.Blue),
        };

        var bounds = path.Bounds;
        bounds.Inflate(strokeWidth / 2f + 1f, strokeWidth / 2f + 1f);
        using var layerPaint = new SKPaint
        {
            Color = new SKColor(0xFF, 0xFF, 0xFF, color.Alpha),
        };

        canvas.SaveLayer(bounds, layerPaint);
        canvas.DrawPath(path, paint);
        canvas.Restore();
    }

    private static void DrawTextAnnotation(SKCanvas canvas, Annotation annotation, SKTypeface typeface)
    {
        if (string.IsNullOrEmpty(annotation.Text))
        {
            return;
        }

        using var font = new SKFont(typeface, annotation.FontSize, 1f, 0f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ToSKColor(annotation.ColorArgb),
            Style = SKPaintStyle.Fill,
        };

        var origin = annotation.Points[0];
        var lineHeight = annotation.FontSize * 1.3f;
        var normalizedText = annotation.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalizedText.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            canvas.DrawText(
                lines[i],
                origin.X,
                origin.Y + annotation.FontSize + lineHeight * i,
                SKTextAlign.Left,
                font,
                paint);
        }
    }

    private static void DrawMosaic(
        SKCanvas canvas,
        Annotation annotation,
        Func<Annotation, SKRect, SKBitmap?>? mosaicResolver)
    {
        if (mosaicResolver is null || annotation.Points.Count < 2)
        {
            return;
        }

        var rect = GetRect(annotation);
        if (rect.Width < 1 || rect.Height < 1)
        {
            return;
        }

        var mosaic = mosaicResolver(annotation, rect);
        if (mosaic is null || mosaic.Width < 1 || mosaic.Height < 1)
        {
            return;
        }

        canvas.Save();
        canvas.ClipRect(rect);
        canvas.DrawBitmap(
            mosaic,
            SKRect.Create(0, 0, mosaic.Width, mosaic.Height),
            rect,
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        canvas.Restore();
    }

    private static void DrawNumber(SKCanvas canvas, Annotation annotation, SKTypeface typeface)
    {
        var center = annotation.Points[0];
        var radius = Math.Max(11f, annotation.FontSize * 0.8f);
        var color = ToSKColor(annotation.ColorArgb);

        using var fillPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = color,
        };
        using var outlinePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1.5f, annotation.StrokeWidth * 0.6f),
            Color = SKColors.White,
        };
        using var font = new SKFont(typeface, annotation.FontSize, 1f, 0f);
        using var textPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
        };

        canvas.DrawCircle(center, radius, fillPaint);
        canvas.DrawCircle(center, radius, outlinePaint);
        canvas.DrawText(
            annotation.Number.ToString(CultureInfo.InvariantCulture),
            center.X,
            center.Y + annotation.FontSize * 0.36f,
            SKTextAlign.Center,
            font,
            textPaint);
    }
}
