using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Clippings.App.Helpers;
using Clippings.App.Services;
using Clippings.Core.Editing;
using Clippings.Core.Models;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Clippings.App.Views;

public partial class AnnotationOverlayWindow : Window
{
    private const float MinimumStrokeDistance = 1.5f;
    private const float MinimumShapeDistance = 3f;
    private const float LaserLifetimeMilliseconds = 400f;

    private static readonly SKTypeface OverlayTypeface =
        SKTypeface.FromFamilyName("Microsoft JhengHei UI") ?? SKTypeface.Default;

    private readonly ScreenAnnotationService _service;
    private readonly MonitorInfo _monitor;
    private readonly LaserTrail _laser = new(LaserLifetimeMilliseconds);
    private readonly DispatcherTimer _laserTimer;

    private Annotation? _draft;
    private List<Vector2>? _draftPoints;
    private bool _erasing;

    public AnnotationOverlayWindow(ScreenAnnotationService service, MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(monitor);

        _service = service;
        _monitor = monitor;

        try
        {
            InitializeComponent();

            var scale = 96.0 / monitor.Dpi;
            Left = monitor.Bounds.X * scale;
            Top = monitor.Bounds.Y * scale;
            Width = monitor.Bounds.Width * scale;
            Height = monitor.Bounds.Height * scale;

            _laserTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33),
            };
            _laserTimer.Tick += OnLaserTimerTick;

            _service.ToolStateChanged += OnToolStateChanged;
            _service.Document.Changed += OnDocumentChanged;
        }
        catch
        {
            _service.ToolStateChanged -= OnToolStateChanged;
            _service.Document.Changed -= OnDocumentChanged;
            throw;
        }
    }

    public IntPtr Handle { get; private set; }

    private int MonitorIndex => _monitor.Index;

    private float EraserTolerance => Math.Max(8f, _service.StrokeWidth * 2f);

    private static double NowMilliseconds => Environment.TickCount64;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Handle = new WindowInteropHelper(this).Handle;
        WindowHelper.SetPhysicalBounds(Handle, _monitor.Bounds);
        WindowHelper.SetClickThrough(Handle, _service.ClickThrough);
        Activate();
        Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _service.ToolStateChanged -= OnToolStateChanged;
        _service.Document.Changed -= OnDocumentChanged;
        _laserTimer.Stop();
        _laserTimer.Tick -= OnLaserTimerTick;
    }

    private void OnActivated(object? sender, EventArgs e) => _service.EnsureToolbarOnTop();

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(BackdropColor());

        if (_service.StrokesVisible)
        {
            foreach (var stroke in _service.Document.GetStrokes(MonitorIndex))
            {
                AnnotationRenderer.Draw(canvas, stroke, OverlayTypeface);
            }
        }

        if (_draft is not null)
        {
            AnnotationRenderer.Draw(canvas, _draft, OverlayTypeface);
        }

        if (_laser.Count > 0)
        {
            var trail = _laser.Prune(NowMilliseconds);
            AnnotationRenderer.DrawLaserTrail(
                canvas,
                trail,
                _service.Color,
                MathF.Max(3f, _service.StrokeWidth));
        }
    }

    private SKColor BackdropColor() => _service.Backdrop switch
    {
        AnnotationBackdrop.Whiteboard => SKColors.White,
        AnnotationBackdrop.Blackboard => SKColors.Black,
        _ => new SKColor(0, 0, 0, 1),
    };

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = ToPoint(ClampToWindow(e.GetPosition(CanvasElement)));

        if (_service.Tool == AnnotationTool.Laser && !_service.ClickThrough)
        {
            _laser.Add(position, NowMilliseconds);
            if (!_laserTimer.IsEnabled)
            {
                _laserTimer.Start();
            }

            CanvasElement.InvalidateVisual();
            return;
        }

        if (_erasing)
        {
            _service.Document.EraseAt(MonitorIndex, position, EraserTolerance);
            return;
        }

        if (_draft is null)
        {
            return;
        }

        if (_draftPoints is not null)
        {
            var last = _draftPoints[^1];
            if (Vector2.Distance(last, position) < MinimumStrokeDistance)
            {
                return;
            }

            _draftPoints.Add(position);
        }
        else
        {
            var start = _draft.Points[0];
            _draft = _draft with { Points = [start, position] };
        }

        CanvasElement.InvalidateVisual();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _service.ClickThrough)
        {
            return;
        }

        var point = ToPoint(ClampToWindow(e.GetPosition(CanvasElement)));
        switch (_service.Tool)
        {
            case AnnotationTool.Laser:
                _laser.Add(point, NowMilliseconds);
                if (!_laserTimer.IsEnabled)
                {
                    _laserTimer.Start();
                }

                break;
            case AnnotationTool.Eraser:
                _erasing = true;
                _service.Document.EraseAt(MonitorIndex, point, EraserTolerance);
                RootGrid.CaptureMouse();
                break;
            case AnnotationTool.Pen:
            case AnnotationTool.Highlighter:
                _draftPoints = [point];
                _draft = new Annotation
                {
                    Tool = _service.Tool,
                    Points = _draftPoints,
                    ColorArgb = AnnotationRenderer.ToAnnotationArgb(_service.Tool, _service.Color),
                    StrokeWidth = _service.StrokeWidth,
                };
                RootGrid.CaptureMouse();
                break;
            default:
                _draft = new Annotation
                {
                    Tool = _service.Tool,
                    Points = [point, point],
                    ColorArgb = AnnotationRenderer.ToAnnotationArgb(_service.Tool, _service.Color),
                    StrokeWidth = _service.StrokeWidth,
                };
                RootGrid.CaptureMouse();
                break;
        }

        CanvasElement.InvalidateVisual();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_erasing)
        {
            _erasing = false;
            RootGrid.ReleaseMouseCapture();
            return;
        }

        if (_draft is null)
        {
            return;
        }

        var draft = _draftPoints is not null
            ? _draft with { Points = [.. _draftPoints] }
            : _draft;
        _draft = null;
        _draftPoints = null;
        RootGrid.ReleaseMouseCapture();

        var start = draft.Points[0];
        var end = draft.Points[^1];
        var isStroke = draft.Tool is AnnotationTool.Pen or AnnotationTool.Highlighter;
        var valid = isStroke
            ? draft.Points.Count > 2
            : Vector2.Distance(start, end) > MinimumShapeDistance;

        if (valid)
        {
            _service.Document.Add(MonitorIndex, draft);
        }

        CanvasElement.InvalidateVisual();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _draft = null;
        _draftPoints = null;
        _erasing = false;
        RootGrid.ReleaseMouseCapture();
        CanvasElement.InvalidateVisual();
        e.Handled = true;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        _erasing = false;
        if (_draft is null)
        {
            return;
        }

        _draft = null;
        _draftPoints = null;
        CanvasElement.InvalidateVisual();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    _service.Document.Undo();
                    e.Handled = true;
                    return;
                case Key.Y:
                    _service.Document.Redo();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Escape)
        {
            _service.Stop();
            e.Handled = true;
            return;
        }

        if (_service.TryHandleShortcut(e.Key))
        {
            e.Handled = true;
        }
    }

    private void OnLaserTimerTick(object? sender, EventArgs e)
    {
        _laser.Prune(NowMilliseconds);
        if (_laser.Count == 0)
        {
            _laserTimer.Stop();
        }

        CanvasElement.InvalidateVisual();
    }

    private void OnToolStateChanged(object? sender, EventArgs e)
    {
        _draft = null;
        _draftPoints = null;
        _erasing = false;
        CanvasElement.InvalidateVisual();
    }

    private void OnDocumentChanged(object? sender, EventArgs e) => CanvasElement.InvalidateVisual();

    private Point ClampToWindow(Point point)
        => new(
            Math.Clamp(point.X, 0, Math.Max(1.0, ActualWidth)),
            Math.Clamp(point.Y, 0, Math.Max(1.0, ActualHeight)));

    private static Vector2 ToPoint(Point point) => new((float)point.X, (float)point.Y);
}
