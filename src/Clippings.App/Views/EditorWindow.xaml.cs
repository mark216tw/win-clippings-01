using System.Globalization;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clippings.App.Helpers;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Editing;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Imaging;
using Clippings.Platform.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SkiaSharp;
using SkiaSharp.Views.Desktop;

namespace Clippings.App.Views;

public partial class EditorWindow : Window
{
    private const double CropHandleSize = 9;
    private const double CropResizeHitThickness = 8;
    private const int MinimumCropSize = 8;
    private const float DefaultZoom = 0.8f;
    private static readonly FontFamily EditorFontFamily = new("Microsoft JhengHei UI");
    private static readonly SKTypeface EditorTypeface = SKTypeface.FromFamilyName("Microsoft JhengHei UI") ?? SKTypeface.Default;

    private readonly List<Annotation> _annotations = [];
    private readonly UndoStack<EditorSnapshot> _undo;
    private readonly Dictionary<Annotation, SKBitmap> _mosaicCache = [];
    private readonly Dictionary<AnnotationTool, RadioButton> _toolButtons = [];
    private readonly ImageFormat _defaultFormat;
    private readonly int _jpegQuality;
    private readonly string _saveDirectory;
    private readonly AppState _state;
    private readonly IClippingsStore _store;
    private readonly ILogger<EditorWindow> _logger;
    private readonly CaptureItem? _origin;
    private readonly bool _canDiscard;

    private SharedEditorBitmap _image;
    private SKColor _color = new(0xFF, 0x3B, 0x30);
    private float _strokeWidth = 4;
    private float _fontSize = 24;
    private AnnotationTool _tool = AnnotationTool.Pan;
    private AnnotationTool _toolBeforeCrop = AnnotationTool.Pan;
    private AnnotationTool _toolBeforeEraser = AnnotationTool.Pan;
    private float _zoom = DefaultZoom;
    private SKPoint _offset;
    private bool _initialZoomApplied;
    private bool _panning;
    private MouseButton? _panButton;
    private Point _panStart;
    private SKPoint _panOrigin;
    private Annotation? _draft;
    private List<Vector2>? _draftPoints;
    private bool _erasing;
    private bool _eraseSnapshotPushed;
    private PixelRect _cropSelection;
    private CropInteraction _cropInteraction;
    private SelectionGrip _cropGrip;
    private SKPoint _cropStartPoint;
    private Point _cropAdjustStartCanvas;
    private PixelRect _cropAdjustStartSelection;
    private bool _busy;
    private bool _colorPopupClosedByLightDismiss;
    private TextBox? _textEditor;
    private SKPoint _textPoint;
    private int _nextNumber = 1;
    private bool _initialized;
    private bool _disposed;
    private bool _restoreMaximized;
    private bool _windowPlacementRestored;

    public EditorWindow(SKBitmap bitmap, AppState state, IClippingsStore store, ILoggerFactory loggerFactory, CaptureItem? origin, bool canDiscard)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var settings = state.Settings;
        _state = state;
        _store = store;
        _logger = loggerFactory.CreateLogger<EditorWindow>();
        _origin = origin;
        _canDiscard = canDiscard;
        _image = new SharedEditorBitmap(bitmap);
        _undo = new UndoStack<EditorSnapshot>(40, snapshot => snapshot.Dispose());
        _defaultFormat = settings.DefaultFormat;
        _jpegQuality = Math.Clamp(settings.JpegQuality, 1, 100);
        _saveDirectory = settings.SaveDirectory;
        _tool = AnnotationTool.Pan;
        _toolBeforeCrop = _tool;
        _toolBeforeEraser = _tool;

        try
        {
            InitializeComponent();

            var editorWindowSettings = settings.EditorWindow;
            if (double.IsFinite(editorWindowSettings.Width) && editorWindowSettings.Width >= MinWidth)
            {
                Width = editorWindowSettings.Width;
            }

            if (double.IsFinite(editorWindowSettings.Height) && editorWindowSettings.Height >= MinHeight)
            {
                Height = editorWindowSettings.Height;
            }

            WindowSizingHelper.ClampToWorkArea(this);
            _restoreMaximized = editorWindowSettings.Maximized;
            if (_restoreMaximized)
            {
                WindowState = WindowState.Maximized;
            }

            DiscardButton.Visibility = _canDiscard ? Visibility.Visible : Visibility.Collapsed;
            CloseButton.Visibility = _canDiscard ? Visibility.Collapsed : Visibility.Visible;

            StrokeCombo.Items.Add(2);
            StrokeCombo.Items.Add(4);
            StrokeCombo.Items.Add(6);
            StrokeCombo.Items.Add(8);
            StrokeCombo.Items.Add(10);
            StrokeCombo.Items.Add(12);
            StrokeCombo.Items.Add(16);
            StrokeCombo.Items.Add(20);
            StrokeCombo.SelectedIndex = 1;

            RegisterToolButtons();
            _initialized = true;
            UpdateToolVisuals();

            UpdateUndoButtons();
            UpdateStatus();
            UpdateOverlayLayout();
        }
        catch
        {
            _disposed = true;
            CancelTextInput();
            ClearMosaicCache();
            _undo?.Clear();
            _image?.Release();
            throw;
        }
    }

    public bool IsDirty { get; private set; }

    private SKBitmap Bitmap => _image.Bitmap;

    private float EraserTolerance => Math.Max(8f, _strokeWidth * 2f);

    private void RegisterToolButtons()
    {
        AddToolButton(AnnotationTool.Pan, PanToolButton);
        AddToolButton(AnnotationTool.Pen, PenToolButton);
        AddToolButton(AnnotationTool.Highlighter, HighlighterToolButton);
        AddToolButton(AnnotationTool.Line, LineToolButton);
        AddToolButton(AnnotationTool.Arrow, ArrowToolButton);
        AddToolButton(AnnotationTool.Rectangle, RectangleToolButton);
        AddToolButton(AnnotationTool.Ellipse, EllipseToolButton);
        AddToolButton(AnnotationTool.Text, TextToolButton);
        AddToolButton(AnnotationTool.Number, NumberToolButton);
        AddToolButton(AnnotationTool.Mosaic, MosaicToolButton);
        AddToolButton(AnnotationTool.Eraser, EraserButton);
        AddToolButton(AnnotationTool.Crop, CropButton);
    }

    private void AddToolButton(AnnotationTool tool, RadioButton button) => _toolButtons[tool] = button;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowHelper.ApplyDarkTitleBar(handle, App.IsDarkTheme);
        WindowHelper.ApplyRoundedCorners(handle);
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (_windowPlacementRestored)
        {
            return;
        }

        if (_restoreMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        _windowPlacementRestored = true;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_windowPlacementRestored || WindowState == WindowState.Minimized)
        {
            return;
        }

        PersistWindowPlacement();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        if (!_initialZoomApplied && CanvasElement.ActualWidth > 1 && CanvasElement.ActualHeight > 1)
        {
            ApplyDefaultZoom();
        }

        UpdateZoomText();
        UpdateOverlayLayout();
    }

    private void ApplyDefaultZoom()
    {
        if (Bitmap.Width <= 0 || Bitmap.Height <= 0)
        {
            return;
        }

        _zoom = DefaultZoom;
        _offset = new SKPoint(
            (float)((CanvasElement.ActualWidth - Bitmap.Width * _zoom) / 2),
            (float)((CanvasElement.ActualHeight - Bitmap.Height * _zoom) / 2));
        _initialZoomApplied = true;
        UpdateZoomText();
        UpdateOverlayLayout();
        CanvasElement.InvalidateVisual();
    }

    private void FitCanvas(bool force = false)
    {
        if (Bitmap.Width <= 0 || Bitmap.Height <= 0)
        {
            return;
        }

        if (!force && _initialZoomApplied)
        {
            return;
        }

        var availableWidth = Math.Max(64, CanvasElement.ActualWidth - 48);
        var availableHeight = Math.Max(64, CanvasElement.ActualHeight - 48);
        _zoom = (float)Math.Min(1.0, Math.Min(availableWidth / Bitmap.Width, availableHeight / Bitmap.Height));
        if (_zoom <= 0 || float.IsNaN(_zoom) || float.IsInfinity(_zoom))
        {
            _zoom = 1;
        }

        _offset = new SKPoint(
            (float)((CanvasElement.ActualWidth - Bitmap.Width * _zoom) / 2),
            (float)((CanvasElement.ActualHeight - Bitmap.Height * _zoom) / 2));
        _initialZoomApplied = true;
        UpdateZoomText();
        UpdateOverlayLayout();
        CanvasElement.InvalidateVisual();
    }

    private void ZoomFromKeyboard(float factor)
    {
        if (CanvasElement.ActualWidth <= 1 || CanvasElement.ActualHeight <= 1)
        {
            return;
        }

        var center = new Point(CanvasElement.ActualWidth / 2, CanvasElement.ActualHeight / 2);
        var imagePoint = ClampToImage(ToImage(center));
        var newZoom = Math.Clamp(_zoom * factor, 0.05f, 8f);
        if (Math.Abs(newZoom - _zoom) < 0.0001f)
        {
            return;
        }

        _zoom = newZoom;
        _offset = new SKPoint(
            (float)(center.X - imagePoint.X * _zoom),
            (float)(center.Y - imagePoint.Y * _zoom));
        UpdateZoomText();
        UpdateOverlayLayout();
        CanvasElement.InvalidateVisual();
    }

    private void UpdateZoomText() => ZoomText.Text = $"{_zoom * 100:0}%";

    private void UpdateStatus()
    {
        ImageSizeText.Text = $"{Bitmap.Width} × {Bitmap.Height}";
        UpdateZoomText();
    }

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var sampling = _zoom >= 1
            ? new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        canvas.Save();
        canvas.Translate(_offset.X, _offset.Y);
        canvas.Scale(_zoom);
        canvas.DrawBitmap(Bitmap, 0, 0, sampling);

        canvas.Save();
        canvas.ClipRect(SKRect.Create(0, 0, Bitmap.Width, Bitmap.Height));
        foreach (var annotation in _annotations)
        {
            AnnotationRenderer.Draw(canvas, annotation, EditorTypeface, ResolveMosaic);
        }

        if (_draft is not null)
        {
            AnnotationRenderer.Draw(canvas, _draft, EditorTypeface, ResolveMosaic);
        }
        canvas.Restore();

        using var borderPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(0.5f, 1f / _zoom),
            Color = new SKColor(0x80, 0x80, 0x80, 0x80),
            IsAntialias = true,
        };
        canvas.DrawRect(SKRect.Create(0, 0, Bitmap.Width, Bitmap.Height), borderPaint);
        canvas.Restore();
    }

    private SKBitmap? ResolveMosaic(Annotation annotation, SKRect rect)
    {
        var bounds = SKRect.Create(0, 0, Bitmap.Width, Bitmap.Height);
        if (!rect.IntersectsWith(bounds))
        {
            return null;
        }

        rect.Intersect(bounds);
        if (rect.Width < 1 || rect.Height < 1)
        {
            return null;
        }

        if (!_mosaicCache.TryGetValue(annotation, out var mosaic))
        {
            var smallWidth = Math.Max(1, (int)(rect.Width / 12));
            var smallHeight = Math.Max(1, (int)(rect.Height / 12));
            mosaic = new SKBitmap(new SKImageInfo(smallWidth, smallHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
            using var mosaicCanvas = new SKCanvas(mosaic);
            mosaicCanvas.DrawBitmap(
                Bitmap,
                rect,
                SKRect.Create(0, 0, smallWidth, smallHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            _mosaicCache[annotation] = mosaic;
        }

        return mosaic;
    }

    private SKPoint ToImage(Point point)
        => new(
            (float)((point.X - _offset.X) / _zoom),
            (float)((point.Y - _offset.Y) / _zoom));

    private bool IsInsideImage(SKPoint point)
        => point.X >= 0 && point.X < Bitmap.Width && point.Y >= 0 && point.Y < Bitmap.Height;

    private SKPoint ClampToImage(SKPoint point)
        => new(
            Math.Clamp(point.X, 0, Bitmap.Width),
            Math.Clamp(point.Y, 0, Bitmap.Height));

    private Rect GetImageCanvasRect()
        => new(_offset.X, _offset.Y, Bitmap.Width * _zoom, Bitmap.Height * _zoom);

    private Rect GetCropCanvasRect()
        => new(
            _offset.X + _cropSelection.X * _zoom,
            _offset.Y + _cropSelection.Y * _zoom,
            _cropSelection.Width * _zoom,
            _cropSelection.Height * _zoom);

    private void UpdateOverlayLayout()
    {
        if (!_initialized || TextOverlay is null || CropOverlay is null)
        {
            return;
        }

        TextOverlay.Clip = new RectangleGeometry(GetImageCanvasRect());
        RenderCropSelection();
    }

    private void RenderCropSelection()
    {
        if (_cropSelection.IsEmpty || _tool != AnnotationTool.Crop)
        {
            CropOverlay.Visibility = Visibility.Collapsed;
            CropActionBar.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = GetCropCanvasRect();
        CropOverlay.Visibility = Visibility.Visible;
        Canvas.SetLeft(CropSelectionBorder, rect.Left);
        Canvas.SetTop(CropSelectionBorder, rect.Top);
        CropSelectionBorder.Width = Math.Max(0, rect.Width);
        CropSelectionBorder.Height = Math.Max(0, rect.Height);

        PositionCropHandle(CropTopLeftHandle, rect.Left, rect.Top);
        PositionCropHandle(CropTopHandle, rect.Left + rect.Width / 2, rect.Top);
        PositionCropHandle(CropTopRightHandle, rect.Right, rect.Top);
        PositionCropHandle(CropLeftHandle, rect.Left, rect.Top + rect.Height / 2);
        PositionCropHandle(CropRightHandle, rect.Right, rect.Top + rect.Height / 2);
        PositionCropHandle(CropBottomLeftHandle, rect.Left, rect.Bottom);
        PositionCropHandle(CropBottomHandle, rect.Left + rect.Width / 2, rect.Bottom);
        PositionCropHandle(CropBottomRightHandle, rect.Right, rect.Bottom);

        var ready = _cropInteraction == CropInteraction.Ready;
        CropHandleLayer.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        CropActionBar.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        if (ready)
        {
            PositionCropActionBar(rect);
        }
    }

    private static void PositionCropHandle(FrameworkElement handle, double x, double y)
    {
        Canvas.SetLeft(handle, x - CropHandleSize / 2);
        Canvas.SetTop(handle, y - CropHandleSize / 2);
    }

    private void PositionCropActionBar(Rect selectionRect)
    {
        CropActionBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = CropActionBar.DesiredSize;
        var left = selectionRect.Right - size.Width;
        var top = selectionRect.Bottom + 8;
        if (top + size.Height > CanvasHost.ActualHeight)
        {
            top = selectionRect.Top - size.Height - 8;
        }

        left = Math.Clamp(left, 4, Math.Max(4, CanvasHost.ActualWidth - size.Width - 4));
        top = Math.Clamp(top, 4, Math.Max(4, CanvasHost.ActualHeight - size.Height - 4));
        Canvas.SetLeft(CropActionBar, left);
        Canvas.SetTop(CropActionBar, top);
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(CanvasElement);

        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right ||
            e.ChangedButton == MouseButton.Left && _tool == AnnotationTool.Pan)
        {
            StartPan(position, e.ChangedButton);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var imagePoint = ToImage(position);
        if (_tool == AnnotationTool.Crop)
        {
            BeginCropInteraction(position, imagePoint);
            e.Handled = true;
            return;
        }

        if (!IsInsideImage(imagePoint))
        {
            return;
        }

        if (_tool == AnnotationTool.Eraser)
        {
            BeginErase(imagePoint);
            e.Handled = true;
            return;
        }

        if (_tool == AnnotationTool.Text)
        {
            BeginTextInput(imagePoint);
            return;
        }

        if (_tool == AnnotationTool.Number)
        {
            AddAnnotation(new Annotation
            {
                Tool = AnnotationTool.Number,
                Points = [imagePoint],
                ColorArgb = AnnotationRenderer.ToArgb(_color),
                StrokeWidth = _strokeWidth,
                FontSize = _fontSize,
                Number = _nextNumber,
            });
            _nextNumber++;
            IsDirty = true;
            return;
        }

        _draftPoints = _tool is AnnotationTool.Pen or AnnotationTool.Highlighter
            ? [imagePoint]
            : null;

        _draft = new Annotation
        {
            Tool = _tool,
            Points = _draftPoints ?? [imagePoint, imagePoint],
            ColorArgb = AnnotationRenderer.ToAnnotationArgb(_tool, _color),
            StrokeWidth = _strokeWidth,
            FontSize = _fontSize,
        };

        CanvasElement.CaptureMouse();
        CanvasElement.InvalidateVisual();
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(CanvasElement);

        if (_panning)
        {
            _offset = new SKPoint(
                _panOrigin.X + (float)(position.X - _panStart.X),
                _panOrigin.Y + (float)(position.Y - _panStart.Y));
            UpdateOverlayLayout();
            CanvasElement.InvalidateVisual();
            return;
        }

        if (_tool == AnnotationTool.Crop)
        {
            HandleCropMouseMove(position);
            return;
        }

        if (_erasing)
        {
            EraseAt(ClampToImage(ToImage(position)));
            return;
        }

        if (_draft is null)
        {
            return;
        }

        var imagePoint = ClampToImage(ToImage(position));
        if (_draftPoints is not null)
        {
            var last = _draftPoints[^1];
            var distance = Vector2.Distance(last, imagePoint);
            if (distance < 1.5f / Math.Max(0.05f, _zoom))
            {
                return;
            }

            _draftPoints.Add(imagePoint);
        }
        else
        {
            var start = _draft.Points[0];
            _draft = _draft with { Points = [start, imagePoint] };
        }

        CanvasElement.InvalidateVisual();
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning && e.ChangedButton == _panButton)
        {
            StopPan(releaseCapture: true);
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && _tool == AnnotationTool.Crop &&
            _cropInteraction is CropInteraction.Selecting or CropInteraction.Moving or CropInteraction.Resizing)
        {
            CompleteCropInteraction();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Left && _erasing)
        {
            EndErase();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left || _draft is null)
        {
            return;
        }

        var draft = _draftPoints is not null
            ? _draft with { Points = [.. _draftPoints] }
            : _draft;
        _draft = null;
        _draftPoints = null;
        CanvasElement.ReleaseMouseCapture();

        var start = draft.Points[0];
        var end = draft.Points[^1];
        var distance = Vector2.Distance(start, end);
        var isStroke = draft.Tool is AnnotationTool.Pen or AnnotationTool.Highlighter;
        var isMosaic = draft.Tool == AnnotationTool.Mosaic;

        var valid = isStroke
            ? draft.Points.Count > 2
            : isMosaic
                ? MathF.Abs(end.X - start.X) > 5 && MathF.Abs(end.Y - start.Y) > 5
                : distance > 3;

        if (valid)
        {
            AddAnnotation(draft);
            IsDirty = true;
        }

        CanvasElement.InvalidateVisual();
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_textEditor is not null ||
            _cropInteraction is CropInteraction.Selecting or CropInteraction.Moving or CropInteraction.Resizing)
        {
            return;
        }

        var position = e.GetPosition(CanvasElement);
        var imagePoint = ClampToImage(ToImage(position));
        var factor = e.Delta > 0 ? 1.12f : 1 / 1.12f;
        var newZoom = Math.Clamp(_zoom * factor, 0.05f, 8f);
        if (Math.Abs(newZoom - _zoom) < 0.0001f)
        {
            return;
        }

        _zoom = newZoom;
        _offset = new SKPoint(
            (float)(position.X - imagePoint.X * _zoom),
            (float)(position.Y - imagePoint.Y * _zoom));
        UpdateZoomText();
        UpdateOverlayLayout();
        CanvasElement.InvalidateVisual();
    }

    private void BeginErase(SKPoint point)
    {
        _erasing = true;
        _eraseSnapshotPushed = false;
        CanvasElement.CaptureMouse();
        EraseAt(point);
    }

    private void EraseAt(SKPoint point)
    {
        List<Annotation>? hits = null;
        var target = new Vector2(point.X, point.Y);
        foreach (var annotation in _annotations)
        {
            if (StrokeHitTester.HitTest(annotation, target, EraserTolerance))
            {
                (hits ??= []).Add(annotation);
            }
        }

        if (hits is null)
        {
            return;
        }

        if (!_eraseSnapshotPushed)
        {
            PushCurrentSnapshot();
            _eraseSnapshotPushed = true;
        }

        foreach (var annotation in hits)
        {
            _annotations.Remove(annotation);
            if (_mosaicCache.Remove(annotation, out var mosaic))
            {
                mosaic.Dispose();
            }
        }

        IsDirty = true;
        UpdateUndoButtons();
        CanvasElement.InvalidateVisual();
    }

    private void EndErase()
    {
        ResetEraseState();
        if (CanvasElement is not null && CanvasElement.IsMouseCaptured)
        {
            CanvasElement.ReleaseMouseCapture();
        }
    }

    private void ResetEraseState()
    {
        _erasing = false;
        _eraseSnapshotPushed = false;
    }

    private void StartPan(Point position, MouseButton button)
    {
        CommitTextInput();
        _panning = true;
        _panButton = button;
        _panStart = position;
        _panOrigin = _offset;
        CanvasElement.Cursor = Cursors.SizeAll;
        CanvasElement.CaptureMouse();
    }

    private void StopPan(bool releaseCapture)
    {
        _panning = false;
        _panButton = null;
        if (CanvasElement is null)
        {
            return;
        }

        if (releaseCapture && CanvasElement.IsMouseCaptured)
        {
            CanvasElement.ReleaseMouseCapture();
        }

        UpdateCanvasCursor();
    }

    private void OnCanvasLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            StopPan(releaseCapture: false);
            return;
        }

        if (_erasing)
        {
            ResetEraseState();
            CanvasElement.InvalidateVisual();
            return;
        }

        if (_cropInteraction is CropInteraction.Selecting or CropInteraction.Moving or CropInteraction.Resizing)
        {
            CancelCropAdjustment(releaseCapture: false);
            return;
        }

        _draft = null;
        _draftPoints = null;
        CanvasElement.InvalidateVisual();
    }

    private void BeginCropInteraction(Point canvasPoint, SKPoint imagePoint)
    {
        if (!_cropSelection.IsEmpty)
        {
            var grip = HitTestCropGrip(canvasPoint);
            if (grip != SelectionGrip.None)
            {
                _cropGrip = grip;
                _cropAdjustStartCanvas = canvasPoint;
                _cropAdjustStartSelection = _cropSelection;
                _cropInteraction = grip == SelectionGrip.Inside
                    ? CropInteraction.Moving
                    : CropInteraction.Resizing;
                CropActionBar.Visibility = Visibility.Collapsed;
                CanvasElement.Cursor = CursorFor(grip);
                CanvasElement.CaptureMouse();
                RenderCropSelection();
                return;
            }
        }

        if (!IsInsideImage(imagePoint))
        {
            return;
        }

        _cropStartPoint = ClampToImage(imagePoint);
        _cropSelection = PixelRect.Empty;
        _cropGrip = SelectionGrip.None;
        _cropInteraction = CropInteraction.Selecting;
        CanvasElement.Cursor = Cursors.Cross;
        CanvasElement.CaptureMouse();
        RenderCropSelection();
    }

    private void HandleCropMouseMove(Point canvasPoint)
    {
        switch (_cropInteraction)
        {
            case CropInteraction.Selecting:
                {
                    var current = ClampToImage(ToImage(canvasPoint));
                    _cropSelection = PixelRect
                        .FromPoints(_cropStartPoint, current)
                        .ClampTo(Bitmap.Width, Bitmap.Height);
                    RenderCropSelection();
                    break;
                }
            case CropInteraction.Moving:
            case CropInteraction.Resizing:
                {
                    var dx = (int)Math.Round(
                        (canvasPoint.X - _cropAdjustStartCanvas.X) / _zoom,
                        MidpointRounding.AwayFromZero);
                    var dy = (int)Math.Round(
                        (canvasPoint.Y - _cropAdjustStartCanvas.Y) / _zoom,
                        MidpointRounding.AwayFromZero);
                    _cropSelection = _cropInteraction == CropInteraction.Moving
                        ? _cropAdjustStartSelection.MoveWithin(dx, dy, Bitmap.Width, Bitmap.Height)
                        : _cropAdjustStartSelection.ResizeWithin(
                            ToResizeEdges(_cropGrip),
                            dx,
                            dy,
                            Bitmap.Width,
                            Bitmap.Height,
                            MinimumCropSize,
                            MinimumCropSize);
                    RenderCropSelection();
                    break;
                }
            case CropInteraction.Ready:
                CanvasElement.Cursor = CursorFor(HitTestCropGrip(canvasPoint));
                break;
            default:
                CanvasElement.Cursor = Cursors.Cross;
                break;
        }
    }

    private void CompleteCropInteraction()
    {
        _cropInteraction = _cropSelection.Width >= MinimumCropSize && _cropSelection.Height >= MinimumCropSize
            ? CropInteraction.Ready
            : CropInteraction.None;
        if (_cropInteraction == CropInteraction.None)
        {
            _cropSelection = PixelRect.Empty;
        }

        _cropGrip = SelectionGrip.None;
        if (CanvasElement.IsMouseCaptured)
        {
            CanvasElement.ReleaseMouseCapture();
        }

        RenderCropSelection();
        UpdateCanvasCursor();
    }

    private void CancelCropAdjustment(bool releaseCapture)
    {
        if (_cropInteraction is CropInteraction.Moving or CropInteraction.Resizing)
        {
            _cropSelection = _cropAdjustStartSelection;
            _cropInteraction = CropInteraction.Ready;
        }
        else
        {
            _cropSelection = PixelRect.Empty;
            _cropInteraction = CropInteraction.None;
        }

        _cropGrip = SelectionGrip.None;
        if (releaseCapture && CanvasElement.IsMouseCaptured)
        {
            CanvasElement.ReleaseMouseCapture();
        }

        RenderCropSelection();
        UpdateCanvasCursor();
    }

    private void ClearCropSelection(bool releaseCapture = true)
    {
        _cropSelection = PixelRect.Empty;
        _cropInteraction = CropInteraction.None;
        _cropGrip = SelectionGrip.None;
        if (releaseCapture && CanvasElement is not null && CanvasElement.IsMouseCaptured)
        {
            CanvasElement.ReleaseMouseCapture();
        }

        if (CropOverlay is not null)
        {
            CropOverlay.Visibility = Visibility.Collapsed;
            CropActionBar.Visibility = Visibility.Collapsed;
        }
    }

    private SelectionGrip HitTestCropGrip(Point point)
    {
        if (_cropSelection.IsEmpty)
        {
            return SelectionGrip.None;
        }

        var rect = GetCropCanvasRect();
        var horizontal = SelectionGrip.None;
        if (point.Y >= rect.Top - CropResizeHitThickness && point.Y <= rect.Bottom + CropResizeHitThickness)
        {
            var toLeft = Math.Abs(point.X - rect.Left);
            var toRight = Math.Abs(point.X - rect.Right);
            if (toLeft <= CropResizeHitThickness && toLeft <= toRight)
            {
                horizontal = SelectionGrip.Left;
            }
            else if (toRight <= CropResizeHitThickness)
            {
                horizontal = SelectionGrip.Right;
            }
        }

        var vertical = SelectionGrip.None;
        if (point.X >= rect.Left - CropResizeHitThickness && point.X <= rect.Right + CropResizeHitThickness)
        {
            var toTop = Math.Abs(point.Y - rect.Top);
            var toBottom = Math.Abs(point.Y - rect.Bottom);
            if (toTop <= CropResizeHitThickness && toTop <= toBottom)
            {
                vertical = SelectionGrip.Top;
            }
            else if (toBottom <= CropResizeHitThickness)
            {
                vertical = SelectionGrip.Bottom;
            }
        }

        return (horizontal, vertical) switch
        {
            (SelectionGrip.Left, SelectionGrip.Top) => SelectionGrip.TopLeft,
            (SelectionGrip.Right, SelectionGrip.Top) => SelectionGrip.TopRight,
            (SelectionGrip.Left, SelectionGrip.Bottom) => SelectionGrip.BottomLeft,
            (SelectionGrip.Right, SelectionGrip.Bottom) => SelectionGrip.BottomRight,
            (SelectionGrip.Left, _) => SelectionGrip.Left,
            (SelectionGrip.Right, _) => SelectionGrip.Right,
            (_, SelectionGrip.Top) => SelectionGrip.Top,
            (_, SelectionGrip.Bottom) => SelectionGrip.Bottom,
            _ => rect.Contains(point) ? SelectionGrip.Inside : SelectionGrip.None,
        };
    }

    private static Cursor CursorFor(SelectionGrip grip) => grip switch
    {
        SelectionGrip.Inside => Cursors.SizeAll,
        SelectionGrip.Left or SelectionGrip.Right => Cursors.SizeWE,
        SelectionGrip.Top or SelectionGrip.Bottom => Cursors.SizeNS,
        SelectionGrip.TopLeft or SelectionGrip.BottomRight => Cursors.SizeNWSE,
        SelectionGrip.TopRight or SelectionGrip.BottomLeft => Cursors.SizeNESW,
        _ => Cursors.Cross,
    };

    private static ResizeEdges ToResizeEdges(SelectionGrip grip) => grip switch
    {
        SelectionGrip.Left => ResizeEdges.Left,
        SelectionGrip.Top => ResizeEdges.Top,
        SelectionGrip.Right => ResizeEdges.Right,
        SelectionGrip.Bottom => ResizeEdges.Bottom,
        SelectionGrip.TopLeft => ResizeEdges.Left | ResizeEdges.Top,
        SelectionGrip.TopRight => ResizeEdges.Right | ResizeEdges.Top,
        SelectionGrip.BottomLeft => ResizeEdges.Left | ResizeEdges.Bottom,
        SelectionGrip.BottomRight => ResizeEdges.Right | ResizeEdges.Bottom,
        _ => ResizeEdges.None,
    };

    private void OnConfirmCropClick(object sender, RoutedEventArgs e)
    {
        if (_cropInteraction != CropInteraction.Ready)
        {
            return;
        }

        var selection = _cropSelection;
        ClearCropSelection();
        ApplyCrop(selection);
    }

    private void OnCancelCropClick(object sender, RoutedEventArgs e) => ClearCropSelection();

    private void BeginTextInput(SKPoint point)
    {
        CommitTextInput();

        _textPoint = point;
        var foreground = AnnotationRenderer.ToSKColor(AnnotationRenderer.ToArgb(_color));
        var availableWidth = Math.Max(40, (Bitmap.Width - point.X) * _zoom);
        var availableHeight = Math.Max(32, (Bitmap.Height - point.Y) * _zoom);
        var editor = new TextBox
        {
            MinWidth = Math.Min(180, availableWidth),
            MaxWidth = availableWidth,
            MaxHeight = availableHeight,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontSize = Math.Max(12, _fontSize * _zoom),
            FontFamily = EditorFontFamily,
            Padding = new Thickness(4, 2, 4, 2),
            Foreground = new SolidColorBrush(Color.FromRgb(foreground.Red, foreground.Green, foreground.Blue)),
            Background = new SolidColorBrush(Color.FromArgb(0xF2, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2D, 0x6B, 0xF2)),
            BorderThickness = new Thickness(1),
        };

        Canvas.SetLeft(editor, point.X * _zoom + _offset.X);
        Canvas.SetTop(editor, point.Y * _zoom + _offset.Y);
        editor.KeyDown += OnTextEditorKeyDown;
        editor.LostKeyboardFocus += OnTextEditorLostFocus;

        _textEditor = editor;
        TextOverlay.IsHitTestVisible = true;
        TextOverlay.Children.Add(editor);
        editor.Focus();
    }

    private void OnTextEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            CommitTextInput();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTextInput();
            e.Handled = true;
        }
    }

    private void OnTextEditorLostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitTextInput();

    private void CommitTextInput()
    {
        var editor = _textEditor;
        if (editor is null)
        {
            return;
        }

        _textEditor = null;
        editor.KeyDown -= OnTextEditorKeyDown;
        editor.LostKeyboardFocus -= OnTextEditorLostFocus;
        TextOverlay.Children.Remove(editor);
        TextOverlay.IsHitTestVisible = false;

        var text = editor.Text.Trim();
        if (text.Length == 0)
        {
            CanvasElement.InvalidateVisual();
            return;
        }

        AddAnnotation(new Annotation
        {
            Tool = AnnotationTool.Text,
            Points = [_textPoint],
            Text = text,
            ColorArgb = AnnotationRenderer.ToAnnotationArgb(_tool, _color),
            StrokeWidth = _strokeWidth,
            FontSize = _fontSize,
        });
        IsDirty = true;
        CanvasElement.InvalidateVisual();
    }

    private void CancelTextInput()
    {
        var editor = _textEditor;
        if (editor is null)
        {
            return;
        }

        _textEditor = null;
        editor.KeyDown -= OnTextEditorKeyDown;
        editor.LostKeyboardFocus -= OnTextEditorLostFocus;
        TextOverlay.Children.Remove(editor);
        TextOverlay.IsHitTestVisible = false;
        CanvasElement.InvalidateVisual();
    }

    private void AddAnnotation(Annotation annotation)
    {
        PushCurrentSnapshot();
        _annotations.Add(annotation);
        UpdateUndoButtons();
        CanvasElement.InvalidateVisual();
    }

    private void PushCurrentSnapshot() => _undo.Push(CaptureSnapshot());

    private EditorSnapshot CaptureSnapshot()
    {
        _image.AddReference();
        return new EditorSnapshot(_image, [.. _annotations], _nextNumber);
    }

    private void RestoreSnapshot(EditorSnapshot snapshot)
    {
        ClearCropSelection();
        ClearMosaicCache();
        var currentImage = _image;
        var restoredImage = snapshot.TakeImage();
        var imageChanged = !ReferenceEquals(currentImage, restoredImage);
        currentImage.Release();
        _image = restoredImage;
        _annotations.Clear();
        _annotations.AddRange(snapshot.Annotations);
        _nextNumber = snapshot.NextNumber;
        UpdateStatus();

        if (imageChanged)
        {
            _initialZoomApplied = false;
            FitCanvas(force: true);
            return;
        }

        UpdateOverlayLayout();
        CanvasElement.InvalidateVisual();
    }

    private void ApplyCrop(PixelRect selection)
    {
        var pixels = selection.ClampTo(Bitmap.Width, Bitmap.Height);

        if (pixels.Width < MinimumCropSize || pixels.Height < MinimumCropSize)
        {
            return;
        }

        var cropped = ImageCodec.Crop(Bitmap, pixels);
        PushCurrentSnapshot();
        _image.Release();
        _image = new SharedEditorBitmap(cropped);

        for (var i = 0; i < _annotations.Count; i++)
        {
            _annotations[i] = _annotations[i].Translate(-pixels.X, -pixels.Y);
        }

        ClearMosaicCache();
        UpdateUndoButtons();
        IsDirty = true;
        _initialZoomApplied = false;
        UpdateStatus();
        FitCanvas(force: true);
    }

    private void OnRotateRightClick(object sender, RoutedEventArgs e) => ApplyRotateFlip(RotateFlipKind.Rotate90Cw);

    private void OnRotateLeftClick(object sender, RoutedEventArgs e) => ApplyRotateFlip(RotateFlipKind.Rotate90Ccw);

    private void OnFlipVerticalClick(object sender, RoutedEventArgs e) => ApplyRotateFlip(RotateFlipKind.FlipVertical);

    private void OnFlipHorizontalClick(object sender, RoutedEventArgs e) => ApplyRotateFlip(RotateFlipKind.FlipHorizontal);

    private void ApplyRotateFlip(RotateFlipKind kind)
    {
        CommitTextInput();
        _draft = null;
        _draftPoints = null;

        if (_tool == AnnotationTool.Crop)
        {
            ApplyTool(_toolBeforeCrop);
        }

        ClearCropSelection();
        StopPan(releaseCapture: true);
        PushCurrentSnapshot();

        SKBitmap? merged = null;
        var source = Bitmap;
        if (_annotations.Count > 0)
        {
            merged = RenderComposite(SKColors.Transparent);
            source = merged;
            _annotations.Clear();
        }

        var transformed = ImageCodec.RotateFlip(source, kind);
        merged?.Dispose();
        _image.Release();
        _image = new SharedEditorBitmap(transformed);

        ClearMosaicCache();
        UpdateUndoButtons();
        IsDirty = true;
        _initialZoomApplied = false;
        UpdateStatus();
        FitCanvas(force: true);
    }

    private void OnToolButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_initialized || sender is not FrameworkElement { Tag: AnnotationTool tool })
        {
            return;
        }

        if (tool == _tool)
        {
            if (tool == AnnotationTool.Eraser)
            {
                ApplyTool(_toolBeforeEraser);
            }
            else if (tool == AnnotationTool.Crop)
            {
                ApplyTool(_toolBeforeCrop);
            }

            return;
        }

        ApplyTool(tool);
    }

    private void ApplyTool(AnnotationTool tool)
    {
        if (!_initialized)
        {
            return;
        }

        if (tool == AnnotationTool.Eraser && _tool != AnnotationTool.Eraser)
        {
            _toolBeforeEraser = _tool == AnnotationTool.Crop ? _toolBeforeCrop : _tool;
        }
        else if (tool == AnnotationTool.Crop && _tool != AnnotationTool.Crop)
        {
            _toolBeforeCrop = _tool == AnnotationTool.Eraser ? _toolBeforeEraser : _tool;
        }

        if (tool != AnnotationTool.Eraser)
        {
            EndErase();
        }

        if (tool != AnnotationTool.Crop)
        {
            ClearCropSelection();
        }

        StopPan(releaseCapture: true);
        _tool = tool;

        if (tool is not (AnnotationTool.Crop or AnnotationTool.Eraser))
        {
            _toolBeforeCrop = tool;
            _toolBeforeEraser = tool;
        }

        _draft = null;
        _draftPoints = null;

        UpdateCanvasCursor();
        UpdateToolVisuals();
        CanvasElement?.InvalidateVisual();
        if (StatusText is not null)
        {
            StatusText.Text = ToolStatusText(tool);
        }
    }

    private void UpdateToolVisuals()
    {
        if (_toolButtons.Count == 0)
        {
            return;
        }

        foreach (var (tool, button) in _toolButtons)
        {
            button.IsChecked = tool == _tool;
        }
    }

    private static string ToolStatusText(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Pan => "使用左鍵拖曳平移畫布，滾輪縮放。",
        AnnotationTool.Text => "按一下畫布輸入文字，Enter 完成，Shift+Enter 換行。",
        AnnotationTool.Crop => "拖曳選取裁切範圍；可移動或調整大小，按確定後套用。",
        AnnotationTool.Mosaic => "拖曳出要模糊的範圍。",
        AnnotationTool.Eraser => "拖曳擦除碰到的註解；Ctrl+Z 可復原。",
        AnnotationTool.Number => "按一下加入編號章。",
        _ => "在圖片內拖曳加入註解；中鍵或右鍵可快速平移，滾輪縮放。",
    };


    private void UpdateCanvasCursor()
    {
        if (CanvasElement is null)
        {
            return;
        }

        CanvasElement.Cursor = _tool switch
        {
            AnnotationTool.Pan => Cursors.Arrow,
            AnnotationTool.Text => Cursors.IBeam,
            AnnotationTool.Crop when _cropInteraction == CropInteraction.Ready => Cursors.Arrow,
            _ => Cursors.Cross,
        };
    }

    private void OnColorMenuClick(object sender, RoutedEventArgs e)
    {
        if (_colorPopupClosedByLightDismiss)
        {
            return;
        }

        ColorPopup.IsOpen = !ColorPopup.IsOpen;
    }

    private void OnColorPopupClosed(object? sender, EventArgs e)
    {
        _colorPopupClosedByLightDismiss = true;
        Dispatcher.BeginInvoke(() => _colorPopupClosedByLightDismiss = false);
    }

    private void OnColorChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Tag: string hex } &&
            uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            _color = new SKColor(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF),
                255);
            var selectedColor = Color.FromRgb(_color.Red, _color.Green, _color.Blue);
            var selectedBrush = new SolidColorBrush(selectedColor);
            ColorMenuButton.Background = selectedBrush;
            ColorMenuButton.Foreground = new SolidColorBrush(GetContrastingTextColor(selectedColor));
            PopupCurrentColorSwatch.Fill = selectedBrush;
            ColorPopup.IsOpen = false;
        }
    }

    private static Color GetContrastingTextColor(Color background)
    {
        var luminance = background.R * 299 + background.G * 587 + background.B * 114;
        return luminance >= 160_000
            ? Color.FromRgb(0x1B, 0x1F, 0x2A)
            : Colors.White;
    }

    private void OnStrokeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StrokeCombo.SelectedItem is int width)
        {
            _strokeWidth = Math.Clamp(width, 1, 20);
        }
    }

    private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();

    private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();

    private void Undo()
    {
        EndErase();
        ClearCropSelection();
        if (!_undo.CanUndo)
        {
            return;
        }

        var previous = _undo.Undo(CaptureSnapshot());
        RestoreSnapshot(previous);
        IsDirty = true;
        UpdateUndoButtons();
        CanvasElement.InvalidateVisual();
    }

    private void Redo()
    {
        EndErase();
        ClearCropSelection();
        if (!_undo.CanRedo)
        {
            return;
        }

        var next = _undo.Redo(CaptureSnapshot());
        RestoreSnapshot(next);
        IsDirty = true;
        UpdateUndoButtons();
        CanvasElement.InvalidateVisual();
    }

    private void UpdateUndoButtons()
    {
        UndoButton.IsEnabled = _undo.CanUndo;
        RedoButton.IsEnabled = _undo.CanRedo;
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => FitCanvas(force: true);

    private void OnCopyClick(object sender, RoutedEventArgs e) => _ = CopyToClipboardAsync();

    private async Task CopyToClipboardAsync()
    {
        if (_busy)
        {
            return;
        }

        var selection = _tool == AnnotationTool.Crop && !_cropSelection.IsEmpty ? _cropSelection : default;
        SetBusy(true);
        try
        {
            var source = await Task.Run(() =>
            {
                if (selection.IsEmpty)
                {
                    if (_annotations.Count == 0)
                    {
                        return BitmapSourceHelper.FromSKBitmap(Bitmap);
                    }

                    using var fullComposite = RenderComposite(SKColors.White);
                    return BitmapSourceHelper.FromSKBitmap(fullComposite);
                }

                if (_annotations.Count == 0)
                {
                    using var cropped = ImageCodec.Crop(Bitmap, selection);
                    return BitmapSourceHelper.FromSKBitmap(cropped);
                }

                using var composite = RenderComposite(SKColors.White);
                using var croppedComposite = ImageCodec.Crop(composite, selection);
                return BitmapSourceHelper.FromSKBitmap(croppedComposite);
            });

            var copied = ClipboardHelper.TrySetImage(source);
            StatusText.Text = !copied
                ? AppStrings.ClipboardCopyFailed
                : selection.IsEmpty ? AppStrings.ClipboardCopied : AppStrings.ClipboardCopiedSelection;
        }
        catch (Exception exception)
        {
            StatusText.Text = "複製失敗：" + exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private SKBitmap RenderComposite(SKColor background)
    {
        var result = new SKBitmap(new SKImageInfo(Bitmap.Width, Bitmap.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(background);
        canvas.DrawBitmap(
            Bitmap,
            SKRect.Create(0, 0, Bitmap.Width, Bitmap.Height),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));

        foreach (var annotation in _annotations)
        {
            AnnotationRenderer.Draw(canvas, annotation, EditorTypeface, ResolveMosaic);
        }

        canvas.Flush();
        return result;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnDiscardClick(object sender, RoutedEventArgs e) => _ = DiscardAndCloseAsync();

    private async Task DiscardAndCloseAsync()
    {
        if (_busy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            if (_origin is { Id: > 0 })
            {
                await _store.DeleteCaptureAsync(_origin.Id);
                (Application.Current.MainWindow as MainWindow)?.NotifyCaptureDeleted();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "捨棄擷圖失敗。");
            StatusText.Text = AppStrings.FormatDiscardFailed(exception.Message);
            MessageBox.Show(
                this,
                StatusText.Text,
                AppStrings.EditorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            SetBusy(false);
            return;
        }

        SetBusy(false);
        Close();
    }

    private void OnSaveAsClick(object sender, RoutedEventArgs e) => _ = SaveToFileAsync();

    private void OnSaveLibraryClick(object sender, RoutedEventArgs e) => _ = SaveToLibraryAndCloseAsync();

    private async Task SaveToFileAsync()
    {
        var defaultJpeg = _defaultFormat == ImageFormat.Jpeg;
        var extension = defaultJpeg ? ".jpg" : ".png";
        var dialog = new SaveFileDialog
        {
            Filter = "PNG 影像 (*.png)|*.png|JPEG 影像 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            FilterIndex = defaultJpeg ? 2 : 1,
            FileName = $"Clippings_{DateTime.Now:yyyyMMdd_HHmmss}{extension}",
            DefaultExt = extension,
            AddExtension = true,
            InitialDirectory = string.IsNullOrWhiteSpace(_saveDirectory) ? null : _saveDirectory,
        };

        if (dialog.ShowDialog(this) != true || _busy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var format = dialog.FilterIndex == 2 ? ImageFormat.Jpeg : ImageFormat.Png;
            var bytes = await Task.Run(() =>
            {
                if (_annotations.Count == 0)
                {
                    return ImageCodec.Encode(Bitmap, format, _jpegQuality);
                }

                using var composite = RenderComposite(SKColors.White);
                return ImageCodec.Encode(composite, format, _jpegQuality);
            });

            await File.WriteAllBytesAsync(dialog.FileName, bytes);
            StatusText.Text = $"{AppStrings.Saved} {dialog.FileName}";
        }
        catch (Exception exception)
        {
            StatusText.Text = "儲存失敗：" + exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        CopyButton.IsEnabled = !busy;
        SaveAsButton.IsEnabled = !busy;
        SaveLibraryButton.IsEnabled = !busy;
        DiscardButton.IsEnabled = !busy;
        CanvasElement.IsEnabled = !busy;

        if (busy)
        {
            UndoButton.IsEnabled = false;
            RedoButton.IsEnabled = false;
        }
        else
        {
            UpdateUndoButtons();
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        if (e.OriginalSource is TextBox)
        {
            return;
        }

        if (e.Key == Key.Enter && _cropInteraction == CropInteraction.Ready)
        {
            OnConfirmCropClick(sender, e);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
        {
            _ = SaveToFileAsync();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.OemPlus or Key.Add)
        {
            ZoomFromKeyboard(1.25f);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.OemMinus or Key.Subtract)
        {
            ZoomFromKeyboard(1 / 1.25f);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key is Key.D0 or Key.NumPad0)
        {
            FitCanvas(force: true);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    Undo();
                    e.Handled = true;
                    return;
                case Key.Y:
                    Redo();
                    e.Handled = true;
                    return;
                case Key.C:
                    _ = CopyToClipboardAsync();
                    e.Handled = true;
                    return;
                case Key.S:
                    _ = SaveToLibraryAndCloseAsync();
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Escape)
        {
            EndErase();
            StopPan(releaseCapture: true);
            _draft = null;
            _draftPoints = null;
            ClearCropSelection();
            CancelTextInput();
            CanvasElement.InvalidateVisual();
            e.Handled = true;
        }
    }

    private void ClearMosaicCache()
    {
        foreach (var bitmap in _mosaicCache.Values)
        {
            bitmap.Dispose();
        }

        _mosaicCache.Clear();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        PersistWindowPlacement();

        if (App.Current.IsExiting)
        {
            return;
        }

        if (_busy)
        {
            e.Cancel = true;
        }
    }

    private async Task SaveToLibraryAndCloseAsync()
    {
        if (_busy)
        {
            return;
        }

        var revealRecentCaptures = _canDiscard;
        CommitTextInput();
        if (!IsDirty && _origin is { Id: > 0 })
        {
            CloseAndRevealRecentCaptures(revealRecentCaptures);
            return;
        }

        SetBusy(true);
        try
        {
            var (imageBytes, thumbnailBytes, width, height) = await Task.Run(() =>
            {
                if (_annotations.Count == 0)
                {
                    return (
                        ImageCodec.Encode(Bitmap, ImageFormat.Png),
                        ImageCodec.EncodeThumbnailJpeg(Bitmap),
                        Bitmap.Width,
                        Bitmap.Height);
                }

                using var composite = RenderComposite(SKColors.Transparent);
                return (
                    ImageCodec.Encode(composite, ImageFormat.Png),
                    ImageCodec.EncodeThumbnailJpeg(composite),
                    composite.Width,
                    composite.Height);
            });

            var updated = _origin is { Id: > 0 }
                ? await _store.UpdateCaptureImageAsync(_origin.Id, imageBytes, "png", thumbnailBytes, width, height)
                : null;
            if (updated is null)
            {
                var capture = new CaptureItem
                {
                    CreatedAt = DateTimeOffset.Now,
                    Width = width,
                    Height = height,
                    Source = _origin?.Source ?? string.Empty,
                };

                await _store.AddCaptureAsync(capture, imageBytes, "png", thumbnailBytes);
                await _store.TrimCapturesAsync(_state.Settings.MaxCaptureItems, _state.Settings.RetentionDays);
            }

            IsDirty = false;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "存入最近擷取失敗。");
            var message = AppStrings.FormatSaveToLibraryFailed(exception.Message);
            StatusText.Text = message;
            MessageBox.Show(
                this,
                message,
                AppStrings.EditorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            SetBusy(false);
            return;
        }

        SetBusy(false);
        if (!revealRecentCaptures)
        {
            (Application.Current.MainWindow as MainWindow)?.NotifyCaptureStored();
        }

        CloseAndRevealRecentCaptures(revealRecentCaptures);
    }

    private void CloseAndRevealRecentCaptures(bool reveal)
    {
        Close();
        if (reveal && Application.Current is App { IsExiting: false } app)
        {
            app.ShowRecentCaptures();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        PersistWindowPlacement();
        CancelTextInput();
        ClearMosaicCache();
        _undo.Clear();
        _image.Release();
    }

    internal void PersistWindowPlacement() => SaveWindowPlacement();

    private void SaveWindowPlacement()
    {
        var maximized = WindowState == WindowState.Maximized;
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth, ActualHeight)
            : RestoreBounds;

        if (!double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height) ||
            bounds.Width < MinWidth || bounds.Height < MinHeight)
        {
            return;
        }

        try
        {
            _state.RememberEditorWindow(bounds.Width, bounds.Height, maximized);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "無法儲存編輯器視窗大小。");
        }
    }

    private enum CropInteraction
    {
        None,
        Selecting,
        Ready,
        Moving,
        Resizing,
    }

    private enum SelectionGrip
    {
        None,
        Inside,
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    private sealed class EditorSnapshot : IDisposable
    {
        private SharedEditorBitmap? _image;

        public EditorSnapshot(SharedEditorBitmap image, IReadOnlyList<Annotation> annotations, int nextNumber)
        {
            _image = image;
            Annotations = annotations;
            NextNumber = nextNumber;
        }

        public IReadOnlyList<Annotation> Annotations { get; }

        public int NextNumber { get; }

        public SharedEditorBitmap TakeImage()
        {
            var image = _image ?? throw new InvalidOperationException("編輯器歷史影像已經釋放。");
            _image = null;
            return image;
        }

        public void Dispose()
        {
            _image?.Release();
            _image = null;
        }
    }

    private sealed class SharedEditorBitmap
    {
        private int _referenceCount = 1;

        public SharedEditorBitmap(SKBitmap bitmap)
        {
            Bitmap = bitmap;
        }

        public SKBitmap Bitmap { get; }

        public void AddReference() => _referenceCount++;

        public void Release()
        {
            _referenceCount--;
            if (_referenceCount == 0)
            {
                Bitmap.Dispose();
            }
        }
    }
}



