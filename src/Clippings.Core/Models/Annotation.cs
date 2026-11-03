using System.Numerics;

namespace Clippings.Core.Models;

public sealed record Annotation
{
    public required AnnotationTool Tool { get; init; }

    public IReadOnlyList<Vector2> Points { get; init; } = [];

    public uint ColorArgb { get; init; } = 0xFFFF3B30;

    public float StrokeWidth { get; init; } = 4f;

    public float FontSize { get; init; } = 24f;

    public string? Text { get; init; }

    public int Number { get; init; }

    public Annotation Translate(float dx, float dy)
    {
        if (Points.Count == 0)
        {
            return this;
        }

        var translated = new Vector2[Points.Count];
        for (var i = 0; i < Points.Count; i++)
        {
            translated[i] = new Vector2(Points[i].X + dx, Points[i].Y + dy);
        }

        return this with { Points = translated };
    }
}
