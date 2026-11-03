namespace Clippings.Core.Models;

public sealed class AnnotationSettings
{
    public AnnotationTool Tool { get; set; } = AnnotationTool.Pen;

    public uint ColorArgb { get; set; } = 0xFFFF3B30;

    public float StrokeWidth { get; set; } = 4f;

    public bool ClickThrough { get; set; }

    public AnnotationBackdrop Backdrop { get; set; } = AnnotationBackdrop.Screen;

    public AnnotationSettings Clone() => (AnnotationSettings)MemberwiseClone();
}
