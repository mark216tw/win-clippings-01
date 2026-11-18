namespace Clippings.Core.Models;

public sealed class AnnotationSettings
{
    public AnnotationTool Tool { get; set; } = AnnotationTool.Pen;

    public uint ColorArgb { get; set; } = 0xFFFF3B30;

    public float StrokeWidth { get; set; } = 6f;

    public bool ClickThrough { get; set; }

    public AnnotationBackdrop Backdrop { get; set; } = AnnotationBackdrop.Screen;

    public AnnotationDockEdge DockEdge { get; set; } = AnnotationDockEdge.None;

    public double DockOffsetRatio { get; set; } = 0.5;

    public bool AutoHideWhenSnapped { get; set; }

    public int AutoHideSeconds { get; set; } = 10;

    public bool MiddleClickToggle { get; set; } = true;

    public AnnotationSettings Clone() => (AnnotationSettings)MemberwiseClone();
}
