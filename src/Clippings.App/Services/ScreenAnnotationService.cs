using Clippings.App.Helpers;
using Clippings.App.Views;
using Clippings.Core.Models;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Windows.Input;

namespace Clippings.App.Services;

public sealed class ScreenAnnotationService
{
    private readonly MonitorService _monitors;
    private readonly AppState _state;
    private readonly ILogger<ScreenAnnotationService> _logger;
    private readonly List<AnnotationOverlayWindow> _overlays = [];
    private AnnotationToolbarWindow? _toolbar;
    private bool _toolbarHiddenForCapture;
    private bool _overlaysHiddenForCapture;

    public ScreenAnnotationService(
        MonitorService monitors,
        AppState state,
        ILogger<ScreenAnnotationService> logger)
    {
        _monitors = monitors;
        _state = state;
        _logger = logger;
    }

    public AnnotationDocument Document { get; private set; } = new();

    public AnnotationTool Tool { get; private set; } = AnnotationTool.Pen;

    public SKColor Color { get; private set; } = new(0xFF, 0x3B, 0x30);

    public float StrokeWidth { get; private set; } = 4f;

    public bool ClickThrough { get; private set; }

    public bool StrokesVisible { get; private set; } = true;

    public AnnotationBackdrop Backdrop { get; private set; } = AnnotationBackdrop.Screen;

    public bool IsActive => _overlays.Count > 0;

    public CaptureCoordinator? CaptureCoordinator { get; set; }

    public event EventHandler? SessionChanged;

    public event EventHandler? ToolStateChanged;

    public void Toggle()
    {
        if (IsActive)
        {
            Stop();
        }
        else
        {
            Start();
        }
    }

    public void Start()
    {
        if (IsActive)
        {
            return;
        }

        var settings = _state.Settings.Annotation.Clone();
        Tool = IsAnnotationTool(settings.Tool) ? settings.Tool : AnnotationTool.Pen;
        Color = AnnotationRenderer.ToSKColor(settings.ColorArgb);
        StrokeWidth = Math.Clamp(settings.StrokeWidth, 1f, 20f);
        ClickThrough = settings.ClickThrough;
        Backdrop = settings.Backdrop;
        StrokesVisible = true;
        Document = new AnnotationDocument();

        var monitors = _monitors.GetMonitors();
        if (monitors.Count == 0)
        {
            _logger.LogWarning("找不到可標註的螢幕。");
            return;
        }

        foreach (var monitor in monitors)
        {
            var overlay = new AnnotationOverlayWindow(this, monitor);
            _overlays.Add(overlay);
        }

        foreach (var overlay in _overlays)
        {
            overlay.Show();
        }

        _toolbar = new AnnotationToolbarWindow(this, _monitors);
        _toolbar.Show();

        ApplyClickThrough();
        _logger.LogInformation("螢幕標註已啟動（{Count} 個螢幕）。", _overlays.Count);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (!IsActive)
        {
            return;
        }

        _state.RememberAnnotation(new AnnotationSettings
        {
            Tool = Tool,
            ColorArgb = AnnotationRenderer.ToArgb(Color),
            StrokeWidth = StrokeWidth,
            ClickThrough = ClickThrough,
            Backdrop = Backdrop,
        });

        foreach (var overlay in _overlays)
        {
            overlay.Close();
        }

        _overlays.Clear();
        _toolbar?.Close();
        _toolbar = null;
        _toolbarHiddenForCapture = false;
        _overlaysHiddenForCapture = false;
        _logger.LogInformation("螢幕標註已結束。");
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleStrokesVisible()
    {
        StrokesVisible = !StrokesVisible;
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TryHandleShortcut(Key key)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return false;
        }

        switch (key)
        {
            case Key.D1:
            case Key.NumPad1:
            case Key.L:
                SetTool(AnnotationTool.Laser);
                return true;
            case Key.D2:
            case Key.NumPad2:
            case Key.P:
                SetTool(AnnotationTool.Pen);
                return true;
            case Key.D3:
            case Key.NumPad3:
            case Key.H:
                SetTool(AnnotationTool.Highlighter);
                return true;
            case Key.D4:
            case Key.NumPad4:
            case Key.N:
                SetTool(AnnotationTool.Line);
                return true;
            case Key.D5:
            case Key.NumPad5:
            case Key.A:
                SetTool(AnnotationTool.Arrow);
                return true;
            case Key.D6:
            case Key.NumPad6:
            case Key.R:
                SetTool(AnnotationTool.Rectangle);
                return true;
            case Key.D7:
            case Key.NumPad7:
            case Key.O:
                SetTool(AnnotationTool.Ellipse);
                return true;
            case Key.D8:
            case Key.NumPad8:
            case Key.E:
                SetTool(AnnotationTool.Eraser);
                return true;
            case Key.D0:
            case Key.NumPad0:
            case Key.C:
                SetClickThrough(!ClickThrough);
                return true;
            case Key.D9:
            case Key.NumPad9:
            case Key.V:
                ToggleStrokesVisible();
                return true;
            default:
                return false;
        }
    }

    public void SetTool(AnnotationTool tool)
    {
        if (!IsAnnotationTool(tool))
        {
            return;
        }

        if (ClickThrough)
        {
            SetClickThrough(false);
        }

        if (Tool == tool)
        {
            return;
        }

        Tool = tool;
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetColor(SKColor color)
    {
        if (Color == color)
        {
            return;
        }

        Color = color;
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetStrokeWidth(float width)
    {
        var clamped = Math.Clamp(width, 1f, 20f);
        if (Math.Abs(StrokeWidth - clamped) < 0.01f)
        {
            return;
        }

        StrokeWidth = clamped;
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetClickThrough(bool clickThrough)
    {
        if (ClickThrough == clickThrough)
        {
            return;
        }

        ClickThrough = clickThrough;
        ApplyClickThrough();
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBackdrop(AnnotationBackdrop backdrop)
    {
        if (Backdrop == backdrop)
        {
            return;
        }

        Backdrop = backdrop;
        ToolStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestCapture()
        => _ = CaptureCoordinator?.CaptureFullScreenAsync(CaptureInvocationSource.UserInterface);

    public void EnsureToolbarOnTop()
    {
        if (_toolbar is null)
        {
            return;
        }

        WindowHelper.BringToTopMost(_toolbar.Handle);
    }

    public void PrepareForCapture()
    {
        if (_toolbar is null || _toolbarHiddenForCapture)
        {
            return;
        }

        _toolbarHiddenForCapture = true;
        _toolbar.Hide();
    }

    public void SuspendOverlays()
    {
        if (_overlaysHiddenForCapture)
        {
            return;
        }

        _overlaysHiddenForCapture = true;
        foreach (var overlay in _overlays)
        {
            overlay.Hide();
        }
    }

    public void ResumeOverlays()
    {
        if (!_overlaysHiddenForCapture)
        {
            return;
        }

        _overlaysHiddenForCapture = false;
        foreach (var overlay in _overlays)
        {
            if (overlay.IsLoaded)
            {
                overlay.Show();
            }
        }
    }

    public void RestoreAfterCapture()
    {
        if (_toolbarHiddenForCapture && _toolbar is not null)
        {
            _toolbarHiddenForCapture = false;
            _toolbar.Show();
        }

        ResumeOverlays();
    }

    private void ApplyClickThrough()
    {
        foreach (var overlay in _overlays)
        {
            WindowHelper.SetClickThrough(overlay.Handle, ClickThrough);
        }
    }

    private static bool IsAnnotationTool(AnnotationTool tool) => tool is
        AnnotationTool.Laser or
        AnnotationTool.Pen or
        AnnotationTool.Highlighter or
        AnnotationTool.Line or
        AnnotationTool.Arrow or
        AnnotationTool.Rectangle or
        AnnotationTool.Ellipse or
        AnnotationTool.Eraser;
}
