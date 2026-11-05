using Clippings.Core.Helpers;
using Clippings.Core.Models;

namespace Clippings.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Clone_IsIndependentCopy()
    {
        var settings = new AppSettings
        {
            DelaySeconds = 5,
            Theme = AppTheme.Dark,
            PlayCaptureSound = true,
            HotkeyWindow = "Ctrl+Shift+W",
        };
        var clone = settings.Clone();

        clone.DelaySeconds = 10;
        clone.PlayCaptureSound = false;
        clone.HotkeyWindow = "Alt+W";

        Assert.Equal(5, settings.DelaySeconds);
        Assert.True(settings.PlayCaptureSound);
        Assert.False(clone.PlayCaptureSound);
        Assert.Equal("Ctrl+Shift+W", settings.HotkeyWindow);
        Assert.Equal("Alt+W", clone.HotkeyWindow);
        Assert.Equal(AppTheme.Dark, clone.Theme);
    }

    [Fact]
    public void PlayCaptureSound_IsEnabledByDefault()
    {
        Assert.True(new AppSettings().PlayCaptureSound);
    }

    [Fact]
    public void CaptureCursor_IsDisabledByDefault()
    {
        Assert.False(new AppSettings().CaptureCursor);
    }

    [Fact]
    public void Clone_CopiesCaptureCursor()
    {
        var settings = new AppSettings { CaptureCursor = true };
        var clone = settings.Clone();

        Assert.True(clone.CaptureCursor);

        clone.CaptureCursor = false;
        Assert.True(settings.CaptureCursor);
    }

    [Fact]
    public void WindowHotkey_HasDefaultGesture()
    {
        Assert.Equal("Ctrl+Shift+W", new AppSettings().HotkeyWindow);
    }

    [Fact]
    public void AnnotateHotkey_HasDefaultGesture()
    {
        Assert.Equal("Ctrl+Shift+A", new AppSettings().HotkeyAnnotate);
    }

    [Fact]
    public void AnnotationSettings_HaveDefaults()
    {
        var annotation = new AppSettings().Annotation;

        Assert.Equal(AnnotationTool.Pen, annotation.Tool);
        Assert.Equal(0xFFFF3B30u, annotation.ColorArgb);
        Assert.Equal(4f, annotation.StrokeWidth);
        Assert.False(annotation.ClickThrough);
        Assert.Equal(AnnotationBackdrop.Screen, annotation.Backdrop);
    }

    [Fact]
    public void Clone_DeepCopiesAnnotationSettings()
    {
        var settings = new AppSettings();
        var clone = settings.Clone();

        clone.Annotation.Tool = AnnotationTool.Laser;
        clone.Annotation.StrokeWidth = 12f;
        clone.Annotation.ClickThrough = true;
        clone.Annotation.Backdrop = AnnotationBackdrop.Whiteboard;

        Assert.Equal(AnnotationTool.Pen, settings.Annotation.Tool);
        Assert.Equal(4f, settings.Annotation.StrokeWidth);
        Assert.False(settings.Annotation.ClickThrough);
        Assert.Equal(AnnotationBackdrop.Screen, settings.Annotation.Backdrop);
    }

    [Fact]
    public void Clone_DeepCopiesRecordingSettings()
    {
        var settings = new AppSettings();
        var clone = settings.Clone();

        clone.Recording.OutputDirectory = @"D:\Recordings";
        clone.Recording.FrameRate = 60;
        clone.Recording.Bitrate = 20_000_000;
        clone.Recording.IncludeCursor = false;
        clone.Recording.IncludeSystemAudio = true;
        clone.Recording.SystemAudioGain = 3.0;
        clone.Recording.DelaySeconds = 20;

        Assert.Equal(string.Empty, settings.Recording.OutputDirectory);
        Assert.Equal(30, settings.Recording.FrameRate);
        Assert.Equal(8_000_000, settings.Recording.Bitrate);
        Assert.True(settings.Recording.IncludeCursor);
        Assert.False(settings.Recording.IncludeSystemAudio);
        Assert.Equal(2.0, settings.Recording.SystemAudioGain);
        Assert.Equal(0, settings.Recording.DelaySeconds);
    }

    [Fact]
    public void EditorWindowSettings_HaveDefaults()
    {
        var editorWindow = new AppSettings().EditorWindow;

        Assert.Equal(1180, editorWindow.Width);
        Assert.Equal(780, editorWindow.Height);
        Assert.False(editorWindow.Maximized);
    }

    [Fact]
    public void Clone_DeepCopiesEditorWindowSettings()
    {
        var settings = new AppSettings();
        var clone = settings.Clone();

        clone.EditorWindow.Width = 1500;
        clone.EditorWindow.Height = 1000;
        clone.EditorWindow.Maximized = true;

        Assert.Equal(1180, settings.EditorWindow.Width);
        Assert.Equal(780, settings.EditorWindow.Height);
        Assert.False(settings.EditorWindow.Maximized);
    }

    [Fact]
    public void RecordingSettings_HaveDefaults()
    {
        var recording = new AppSettings().Recording;

        Assert.Equal(string.Empty, recording.OutputDirectory);
        Assert.Equal(30, recording.FrameRate);
        Assert.Equal(8_000_000, recording.Bitrate);
        Assert.True(recording.IncludeCursor);
        Assert.False(recording.IncludeSystemAudio);
        Assert.Equal(2.0, recording.SystemAudioGain);
        Assert.Equal(0, recording.DelaySeconds);
    }

    [Fact]
    public void AnnotationTool_ExistingValuesRemainStable()
    {
        Assert.Equal(0, (int)AnnotationTool.Arrow);
        Assert.Equal(1, (int)AnnotationTool.Line);
        Assert.Equal(2, (int)AnnotationTool.Rectangle);
        Assert.Equal(3, (int)AnnotationTool.Ellipse);
        Assert.Equal(4, (int)AnnotationTool.Pen);
        Assert.Equal(5, (int)AnnotationTool.Highlighter);
        Assert.Equal(6, (int)AnnotationTool.Text);
        Assert.Equal(7, (int)AnnotationTool.Mosaic);
        Assert.Equal(8, (int)AnnotationTool.Number);
        Assert.Equal(9, (int)AnnotationTool.Crop);
        Assert.Equal(10, (int)AnnotationTool.Pan);
        Assert.Equal(11, (int)AnnotationTool.Laser);
        Assert.Equal(12, (int)AnnotationTool.Eraser);
    }

    [Fact]
    public void AnnotationBackdrop_ExistingValuesRemainStable()
    {
        Assert.Equal(0, (int)AnnotationBackdrop.Screen);
        Assert.Equal(1, (int)AnnotationBackdrop.Whiteboard);
        Assert.Equal(2, (int)AnnotationBackdrop.Blackboard);
    }

    [Fact]
    public void AppTheme_ExistingValuesRemainStable()
    {
        Assert.Equal(0, (int)AppTheme.System);
        Assert.Equal(1, (int)AppTheme.Light);
        Assert.Equal(2, (int)AppTheme.Dark);
        Assert.Equal(3, (int)AppTheme.WarmPaper);
        Assert.Equal(4, (int)AppTheme.SoftSage);
        Assert.Equal(5, (int)AppTheme.DuskBlue);
        Assert.Equal(6, (int)AppTheme.DuskPurple);
        Assert.Equal(7, (int)AppTheme.InkGreenNight);
        Assert.Equal(8, (int)AppTheme.MistBlueMorning);
    }

    [Fact]
    public void CaptureMode_ExistingValuesRemainStable()
    {
        Assert.Equal(0, (int)CaptureMode.Region);
        Assert.Equal(1, (int)CaptureMode.FullScreen);
        Assert.Equal(2, (int)CaptureMode.Monitor);
        Assert.Equal(3, (int)CaptureMode.Delayed);
        Assert.Equal(4, (int)CaptureMode.Window);
    }

    [Theory]
    [InlineData(AppTheme.WarmPaper)]
    [InlineData(AppTheme.SoftSage)]
    [InlineData(AppTheme.DuskBlue)]
    [InlineData(AppTheme.DuskPurple)]
    [InlineData(AppTheme.InkGreenNight)]
    [InlineData(AppTheme.MistBlueMorning)]
    public void Clone_PreservesComfortTheme(AppTheme theme)
    {
        var clone = new AppSettings { Theme = theme }.Clone();

        Assert.Equal(theme, clone.Theme);
    }

    [Fact]
    public void Sha256Hex_IsStable()
    {
        Assert.Equal(
            "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824",
            HashUtil.Sha256Hex("hello"));
    }

    [Fact]
    public void BuildImageFileName_UsesTimestampAndHashPrefix()
    {
        var name = PathUtil.BuildImageFileName(new DateTimeOffset(2026, 10, 31, 9, 5, 1, TimeSpan.Zero), "abcdef1234567890", "png");

        Assert.Equal("Clippings_20261031_090501_abcdef12.png", name);
    }

    [Fact]
    public void BuildRecordingFileName_UsesTimestamp()
    {
        var name = PathUtil.BuildRecordingFileName(
            new DateTimeOffset(2026, 11, 3, 14, 6, 9, TimeSpan.Zero));

        Assert.Equal("Clippings_20261103_140609.mp4", name);
    }

    [Fact]
    public void RecordingState_ValuesRemainStable()
    {
        Assert.Equal(0, (int)RecordingState.Idle);
        Assert.Equal(1, (int)RecordingState.Starting);
        Assert.Equal(2, (int)RecordingState.Recording);
        Assert.Equal(3, (int)RecordingState.Stopping);
        Assert.Equal(4, (int)RecordingState.Countdown);
        Assert.Equal(5, (int)RecordingState.Paused);
    }
}
