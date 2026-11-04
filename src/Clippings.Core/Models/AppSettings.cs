namespace Clippings.Core.Models;

public sealed class AppSettings
{
    public string HotkeyRegion { get; set; } = "Ctrl+Shift+X";

    public string HotkeyFullScreen { get; set; } = "Ctrl+Shift+F";

    public string HotkeyWindow { get; set; } = "Ctrl+Shift+W";

    public string HotkeyDelayed { get; set; } = "Ctrl+Shift+D";

    public string HotkeyAnnotate { get; set; } = "Ctrl+Shift+A";

    public int DelaySeconds { get; set; } = 5;

    public string SaveDirectory { get; set; } = string.Empty;

    public ImageFormat DefaultFormat { get; set; } = ImageFormat.Png;

    public int JpegQuality { get; set; } = 90;

    public bool AutoCopy { get; set; } = true;

    public bool PlayCaptureSound { get; set; } = true;

    public bool SaveAutomatically { get; set; }

    public bool ClipboardHistoryEnabled { get; set; } = true;

    public int MaxCaptureItems { get; set; } = 200;

    public int MaxClipboardItems { get; set; } = 200;

    public int RetentionDays { get; set; } = 30;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool StartWithWindows { get; set; }

    public bool ShowMagnifier { get; set; } = true;

    public AnnotationSettings Annotation { get; set; } = new();

    public RecordingSettings Recording { get; set; } = new();

    public EditorWindowSettings EditorWindow { get; set; } = new();

    public AppSettings Clone()
    {
        var clone = (AppSettings)MemberwiseClone();
        clone.Annotation = Annotation.Clone();
        clone.Recording = Recording.Clone();
        clone.EditorWindow = EditorWindow.Clone();
        return clone;
    }
}

public sealed class EditorWindowSettings
{
    public double Width { get; set; } = 1180;

    public double Height { get; set; } = 780;

    public bool Maximized { get; set; }

    public EditorWindowSettings Clone() => (EditorWindowSettings)MemberwiseClone();
}

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
    WarmPaper = 3,
    SoftSage = 4,
    DuskBlue = 5,
    DuskPurple = 6,
    InkGreenNight = 7,
    MistBlueMorning = 8,
}

