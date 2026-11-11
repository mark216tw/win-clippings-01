using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Clippings.App.Strings;
using Clippings.Core.Editing;
using Clippings.Core.Models;
using Clippings.Platform.Display;
using Clippings.Platform.Imaging;
using Clippings.Platform.Interop;
using SkiaSharp;

namespace Clippings.App.Views;

public enum OverlayAction
{
    Copy,
    Edit,
    Save,
}

public sealed class OverlayCompletedEventArgs : EventArgs
{
    public OverlayCompletedEventArgs(SKBitmap image, PixelRect bounds, OverlayAction action)
    {
        Image = image;
        Bounds = bounds;
        Action = action;
    }

    public SKBitmap Image { get; }

    public PixelRect Bounds { get; }

    public OverlayAction Action { get; }
}

public partial class CaptureOverlayWindow : Window
{
    private const double CrosshairArm = 18;
    private const double CrosshairGap = 3;
    private const double ResizeHitThickness = 8;
    private const double HandleSize = 9;
    private const int MinimumSelectionSize = 4;

    private static readonly Brush HandleStroke = new SolidColorBrush(Color.FromRgb(0x2D, 0x6B, 0xF2));

    private readonly SKBitmap _bitmap;
    private readonly BitmapSource _bitmapSource;
    private readonly MonitorInfo _monitor;
    private readonly bool _showMagnifier;

    private Point _dragStart;
    private Point _dragCurrent;
    private bool _selecting;
    private bool _hasSelection;
    private bool _adjusting;
    private bool _completing;
    private Point _adjustStart;
    private PixelRect _adjustStartSelection;
    private PixelRect _selection;
    private SelectionGrip _activeGrip;
    private Size _magnifierSize = Size.Empty;



    public CaptureOverlayWindow(
        MonitorInfo monitor,
        SKBitmap bitmap,
        BitmapSource bitmapSource,
        bool showMagnifier)
    {
        InitializeComponent();

        _monitor = monitor;
        _bitmap = bitmap;
        _bitmapSource = bitmapSource;
        _showMagnifier = showMagnifier;

        FrozenImage.Source = _bitmapSource;

        var scale = 96.0 / monitor.Dpi;

        Left = monitor.Bounds.X * scale;
        Top = monitor.Bounds.Y * scale;
        Width = monitor.Bounds.Width * scale;
        Height = monitor.Bounds.Height * scale;
    }

    public event EventHandler<OverlayCompletedEventArgs>? Completed;

    public event EventHandler? Cancelled;

    public bool IsCompleted => _completing;

    public MonitorInfo Monitor => _monitor;

    private double ScaleX => _bitmap.Width / Math.Max(1.0, ActualWidth);

    private double ScaleY => _bitmap.Height / Math.Max(1.0, ActualHeight);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowHelper.SetPhysicalBounds(handle, _monitor.Bounds);
        WindowHelper.ExcludeFromCapture(handle);
        UpdateLayout();
        CaptureHint.Visibility = Visibility.Visible;
        MagnifierPanel.Visibility = Visibility.Collapsed;
        CursorLayer.Visibility = Visibility.Collapsed;
        UpdateDim();
        Activate();
        Focus();
    }


    private void OnClosed(object? sender, EventArgs e)
    {
        ReleaseMouseCapture();

        if (!_completing)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDim();
        if (_hasSelection)
        {
            RenderSelection();
        }
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ActionBar.Visibility == Visibility.Visible && ActionBar.IsMouseOver)
        {
            return;
        }

        if (e.OriginalSource is Button)
        {
            return;
        }

        var point = ClampToWindow(e.GetPosition(this));
        if (_hasSelection)
        {
            var grip = HitTestGrip(point);
            if (grip != SelectionGrip.None)
            {
                BeginAdjust(point, grip);
                e.Handled = true;
                return;
            }
        }

        BeginDrawing(point);
    }

    private void BeginDrawing(Point point)
    {
        _adjusting = false;
        _activeGrip = SelectionGrip.None;
        _selecting = true;
        _hasSelection = false;
        _dragStart = point;
        _dragCurrent = point;
        CaptureHint.Visibility = Visibility.Collapsed;
        ActionBar.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
        HandleLayer.Visibility = Visibility.Collapsed;
        _selection = PixelRect.Empty;
        CaptureMouse();
        SetCrosshairCursor(point);
        UpdateDim();
    }

    private void BeginAdjust(Point point, SelectionGrip grip)
    {
        _adjusting = true;
        _activeGrip = grip;
        _adjustStart = point;
        _adjustStartSelection = _selection;
        ActionBar.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
        HandleLayer.Visibility = Visibility.Collapsed;
        CaptureMouse();
        UpdatePointer(point);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var position = ClampToWindow(e.GetPosition(this));

        if (_selecting)
        {
            _dragCurrent = position;
            _selection = BuildSelection();
            SetCrosshairCursor(position);
            UpdateSelectionVisuals();
            UpdateMagnifier(position);
            return;
        }

        if (_adjusting)
        {
            UpdateAdjust(position);
            UpdatePointer(position);
            return;
        }

        UpdatePointer(position);
    }

    private void UpdatePointer(Point position)
    {
        var grip = _adjusting
            ? _activeGrip
            : _hasSelection
                ? HitTestGrip(position)
                : SelectionGrip.None;

        if (grip == SelectionGrip.None)
        {
            SetCrosshairCursor(position);
            return;
        }

        CursorLayer.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
        Cursor = CursorFor(grip);
    }

    private void SetCrosshairCursor(Point position)
    {
        Cursor = Cursors.None;
        UpdateCrosshair(position);
    }

    private void UpdateAdjust(Point position)
    {
        var dx = ToPixelDelta(position.X - _adjustStart.X, ScaleX);
        var dy = ToPixelDelta(position.Y - _adjustStart.Y, ScaleY);
        _selection = _activeGrip == SelectionGrip.Inside
            ? _adjustStartSelection.MoveWithin(dx, dy, _bitmap.Width, _bitmap.Height)
            : _adjustStartSelection.ResizeWithin(
                ToResizeEdges(_activeGrip),
                dx,
                dy,
                _bitmap.Width,
                _bitmap.Height,
                MinimumSelectionSize,
                MinimumSelectionSize);

        RenderSelection();
    }

    private static int ToPixelDelta(double dipDelta, double scale)
        => (int)Math.Round(dipDelta * scale, MidpointRounding.AwayFromZero);

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_selecting || _adjusting)
        {
            return;
        }

        Cursor = Cursors.None;
        CursorLayer.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
    }

    private void OnActionBarMouseEnter(object sender, MouseEventArgs e)
    {
        Cursor = Cursors.Arrow;
        CursorLayer.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
    }

    private void OnActionBarMouseLeave(object sender, MouseEventArgs e)
    {
        Cursor = Cursors.None;
        CursorLayer.Visibility = Visibility.Collapsed;
    }

    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_adjusting)
        {
            CancelAdjust();
            return;
        }

        if (_selecting)
        {
            _selecting = false;
            ResetSelection();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_adjusting)
        {
            _adjusting = false;
            ReleaseMouseCapture();
            _hasSelection = !_selection.IsEmpty;
            RenderSelection();
            ShowActionBar();
            UpdatePointer(ClampToWindow(e.GetPosition(this)));
            return;
        }

        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        ReleaseMouseCapture();
        MagnifierPanel.Visibility = Visibility.Collapsed;
        _selection = BuildSelection();


        if (_selection.Width < MinimumSelectionSize || _selection.Height < MinimumSelectionSize)
        {
            ResetSelection();
            return;
        }

        _hasSelection = true;
        RenderSelection();
        ShowActionBar();
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_adjusting)
        {
            CancelAdjust();
            return;
        }

        if (_hasSelection)
        {
            ResetSelection();
            return;
        }

        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_adjusting)
            {
                CancelAdjust();
            }
            else if (_hasSelection)
            {
                ResetSelection();
            }
            else
            {
                Close();
            }

            e.Handled = true;
            return;
        }

        if (!_hasSelection)
        {
            return;
        }

        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        var moveX = e.Key switch
        {
            Key.Left => -step,
            Key.Right => step,
            _ => 0,
        };
        var moveY = e.Key switch
        {
            Key.Up => -step,
            Key.Down => step,
            _ => 0,
        };

        if (moveX != 0 || moveY != 0)
        {
            _selection = _selection.MoveWithin(moveX, moveY, _bitmap.Width, _bitmap.Height);
            RenderSelection();
            ShowActionBar();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            Complete(OverlayAction.Edit);
            e.Handled = true;
        }
        else if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Complete(OverlayAction.Edit);
            e.Handled = true;
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e) => Complete(OverlayAction.Edit);

    private void OnCancelClick(object sender, RoutedEventArgs e) => ResetSelection();

    private void Complete(OverlayAction action)
    {
        if (_completing || !_hasSelection)
        {
            return;
        }

        _completing = true;
        var cropped = ImageCodec.Crop(_bitmap, _selection);
        var bounds = _selection.Offset(_monitor.Bounds.X, _monitor.Bounds.Y);
        Completed?.Invoke(this, new OverlayCompletedEventArgs(cropped, bounds, action));
        Close();
    }

    private Point ClampToWindow(Point point)
        => new(
            Math.Clamp(point.X, 0, Math.Max(1.0, ActualWidth)),
            Math.Clamp(point.Y, 0, Math.Max(1.0, ActualHeight)));

    private PixelRect BuildSelection()
    {
        var scaleX = ScaleX;
        var scaleY = ScaleY;
        var startX = (float)(_dragStart.X * scaleX);
        var startY = (float)(_dragStart.Y * scaleY);
        var endX = (float)(_dragCurrent.X * scaleX);
        var endY = (float)(_dragCurrent.Y * scaleY);

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var dx = endX - startX;
            var dy = endY - startY;
            var size = Math.Max(Math.Abs(dx), Math.Abs(dy));
            endX = startX + (dx < 0 ? -size : size);
            endY = startY + (dy < 0 ? -size : size);
        }

        var start = new System.Numerics.Vector2(startX, startY);
        var end = new System.Numerics.Vector2(endX, endY);
        return PixelRect.FromPoints(start, end).ClampTo(_bitmap.Width, _bitmap.Height);
    }

    private Rect GetDipRect() => new(
        _selection.X / ScaleX,
        _selection.Y / ScaleY,
        _selection.Width / ScaleX,
        _selection.Height / ScaleY);

    private void UpdateSelectionVisuals()
    {
        if (_selection.IsEmpty)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            UpdateDim();
            return;
        }

        RenderSelection();
        ActionBar.Visibility = Visibility.Collapsed;
    }

    private void RenderSelection()
    {
        var rect = GetDipRect();
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Margin = new Thickness(rect.X, rect.Y, 0, 0);
        SelectionBorder.Width = Math.Max(0, rect.Width);
        SelectionBorder.Height = Math.Max(0, rect.Height);
        SelectionBorder.HorizontalAlignment = HorizontalAlignment.Left;
        SelectionBorder.VerticalAlignment = VerticalAlignment.Top;

        SizeText.Text = AppStrings.FormatSize(_selection.Width, _selection.Height);
        SizeLabel.Visibility = Visibility.Visible;
        var labelSize = MeasureContentSize(SizeLabel);
        var labelX = Math.Clamp(rect.X, 4, Math.Max(4, ActualWidth - labelSize.Width - 4));
        var labelY = rect.Y - labelSize.Height - 6;
        if (labelY < 4)
        {
            labelY = Math.Min(ActualHeight - labelSize.Height - 4, rect.Y + 6);
        }

        SizeLabel.Margin = new Thickness(labelX, labelY, 0, 0);

        UpdateHandles(rect);
        UpdateDim();
    }

    private void UpdateHandles(Rect rect)
    {
        if (!_hasSelection || _selecting || _adjusting)
        {
            HandleLayer.Visibility = Visibility.Collapsed;
            return;
        }

        if (HandleLayer.Children.Count != 8)
        {
            HandleLayer.Children.Clear();
            for (var i = 0; i < 8; i++)
            {
                HandleLayer.Children.Add(CreateHandle());
            }
        }

        var points = HandlePoints(rect);
        for (var i = 0; i < points.Length; i++)
        {
            var handle = (Rectangle)HandleLayer.Children[i];
            Canvas.SetLeft(handle, points[i].X - (HandleSize / 2));
            Canvas.SetTop(handle, points[i].Y - (HandleSize / 2));
        }

        HandleLayer.Visibility = Visibility.Visible;
    }

    private static Rectangle CreateHandle() => new()
    {
        Width = HandleSize,
        Height = HandleSize,
        RadiusX = HandleSize / 2,
        RadiusY = HandleSize / 2,
        Fill = Brushes.White,
        Stroke = HandleStroke,
        StrokeThickness = 1.5,
    };

    private static Point[] HandlePoints(Rect rect) =>
    [
        new Point(rect.Left, rect.Top),
        new Point(rect.Left + (rect.Width / 2), rect.Top),
        new Point(rect.Right, rect.Top),
        new Point(rect.Right, rect.Top + (rect.Height / 2)),
        new Point(rect.Right, rect.Bottom),
        new Point(rect.Left + (rect.Width / 2), rect.Bottom),
        new Point(rect.Left, rect.Bottom),
        new Point(rect.Left, rect.Top + (rect.Height / 2)),
    ];

    private void ShowActionBar()
    {
        ActionBar.Visibility = Visibility.Visible;
        var size = MeasureContentSize(ActionBar);
        var rect = GetDipRect();

        var x = rect.Right - size.Width;
        var y = rect.Bottom + 8;
        if (y + size.Height > ActualHeight - 8)
        {
            y = rect.Top - size.Height - 8;
        }

        x = Math.Clamp(x, 8, Math.Max(8, ActualWidth - size.Width - 8));
        y = Math.Clamp(y, 8, Math.Max(8, ActualHeight - size.Height - 8));
        ActionBar.Margin = new Thickness(x, y, 0, 0);

        CaptureHint.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
    }


    private void UpdateMagnifier(Point position)
    {
        if (!_showMagnifier || !_selecting || _hasSelection || _bitmap.Width <= 0 || _bitmap.Height <= 0)
        {
            MagnifierPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var dragDistance = Math.Abs(_dragCurrent.X - _dragStart.X) + Math.Abs(_dragCurrent.Y - _dragStart.Y);
        if (dragDistance < 6)
        {
            MagnifierPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var scaleX = ScaleX;
        var scaleY = ScaleY;
        var px = (int)Math.Clamp(Math.Floor(position.X * scaleX), 0, _bitmap.Width - 1);
        var py = (int)Math.Clamp(Math.Floor(position.Y * scaleY), 0, _bitmap.Height - 1);

        const int span = 21;
        var viewWidth = Math.Min(span, _bitmap.Width);
        var viewHeight = Math.Min(span, _bitmap.Height);
        var viewX = Math.Clamp(px - span / 2, 0, Math.Max(0, _bitmap.Width - viewWidth));
        var viewY = Math.Clamp(py - span / 2, 0, Math.Max(0, _bitmap.Height - viewHeight));

        var cropped = new CroppedBitmap(_bitmapSource, new Int32Rect(viewX, viewY, viewWidth, viewHeight));
        cropped.Freeze();
        MagnifierImage.Source = cropped;

        MagnifierCoords.Text = $"{px}, {py}";

        var color = _bitmap.GetPixel(px, py);
        ColorPreview.Background = new SolidColorBrush(Color.FromRgb(color.Red, color.Green, color.Blue));
        MagnifierColor.Text = $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

        if (MagnifierPanel.Visibility != Visibility.Visible)
        {
            MagnifierPanel.Visibility = Visibility.Visible;
            _magnifierSize = MeasureContentSize(MagnifierPanel);
        }

        var size = _magnifierSize.Width > 1 && _magnifierSize.Height > 1
            ? _magnifierSize
            : new Size(152, 170);

        const double margin = 16;
        var x = Math.Max(margin, ActualWidth - size.Width - margin);
        var y = margin;

        var magnifierRect = new Rect(x, y, size.Width, size.Height);
        var selectionRect = GetDipRect();
        if (magnifierRect.Contains(position) || magnifierRect.IntersectsWith(selectionRect))
        {
            MagnifierPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var offset = new Thickness(Math.Round(x), Math.Round(y), 0, 0);
        if (MagnifierPanel.Margin != offset)
        {
            MagnifierPanel.Margin = offset;
        }
    }

    private static Size MeasureContentSize(FrameworkElement element)
    {
        var margin = element.Margin;
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new Size(
            Math.Max(0, element.DesiredSize.Width - margin.Left - margin.Right),
            Math.Max(0, element.DesiredSize.Height - margin.Top - margin.Bottom));
    }

    private void UpdateCrosshair(Point position)
    {
        if (double.IsNaN(position.X) || double.IsNaN(position.Y) || _bitmap.Width <= 0 || _bitmap.Height <= 0)
        {
            CursorLayer.Visibility = Visibility.Collapsed;
            return;
        }

        var x = position.X;
        var y = position.Y;
        SetLine(CursorHaloH1, x - CrosshairArm, y, x - CrosshairGap, y);
        SetLine(CursorHaloH2, x + CrosshairGap, y, x + CrosshairArm, y);
        SetLine(CursorHaloV1, x, y - CrosshairArm, x, y - CrosshairGap);
        SetLine(CursorHaloV2, x, y + CrosshairGap, x, y + CrosshairArm);
        SetLine(CursorCoreH1, x - CrosshairArm, y, x - CrosshairGap, y);
        SetLine(CursorCoreH2, x + CrosshairGap, y, x + CrosshairArm, y);
        SetLine(CursorCoreV1, x, y - CrosshairArm, x, y - CrosshairGap);
        SetLine(CursorCoreV2, x, y + CrosshairGap, x, y + CrosshairArm);
        CursorLayer.Visibility = Visibility.Visible;
    }

    private static void SetLine(Line line, double x1, double y1, double x2, double y2)
    {
        line.X1 = x1;
        line.Y1 = y1;
        line.X2 = x2;
        line.Y2 = y2;
    }


    private void UpdateDim()
    {
        var showDim = (_selecting || _hasSelection) && !_selection.IsEmpty;
        if (!showDim)
        {
            DimLayer.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = GetDipRect();
        var width = Math.Max(0, ActualWidth);
        var height = Math.Max(0, ActualHeight);
        var left = Math.Clamp(rect.Left, 0, width);
        var top = Math.Clamp(rect.Top, 0, height);
        var right = Math.Clamp(rect.Right, 0, width);
        var bottom = Math.Clamp(rect.Bottom, 0, height);

        SetDimRect(DimTop, 0, 0, width, top);
        SetDimRect(DimBottom, 0, bottom, width, height - bottom);
        SetDimRect(DimLeft, 0, top, left, bottom - top);
        SetDimRect(DimRight, right, top, width - right, bottom - top);

        DimLayer.Visibility = Visibility.Visible;
    }

    private static void SetDimRect(Rectangle rectangle, double x, double y, double width, double height)
    {
        if (width <= 0.5 || height <= 0.5)
        {
            rectangle.Visibility = Visibility.Collapsed;
            return;
        }

        Canvas.SetLeft(rectangle, x);
        Canvas.SetTop(rectangle, y);
        rectangle.Width = width;
        rectangle.Height = height;
        rectangle.Visibility = Visibility.Visible;
    }


    private void ResetSelection()
    {
        _hasSelection = false;
        _selecting = false;
        _adjusting = false;
        _activeGrip = SelectionGrip.None;
        _selection = PixelRect.Empty;
        ReleaseMouseCapture();
        SelectionBorder.Visibility = Visibility.Collapsed;
        SizeLabel.Visibility = Visibility.Collapsed;
        ActionBar.Visibility = Visibility.Collapsed;
        MagnifierPanel.Visibility = Visibility.Collapsed;
        HandleLayer.Visibility = Visibility.Collapsed;
        CaptureHint.Visibility = Visibility.Visible;
        Cursor = Cursors.None;
        UpdateDim();
    }

    private void CancelAdjust()
    {
        if (!_adjusting)
        {
            return;
        }

        _adjusting = false;
        _activeGrip = SelectionGrip.None;
        _selection = _adjustStartSelection;
        _hasSelection = !_selection.IsEmpty;
        ReleaseMouseCapture();
        RenderSelection();
        ShowActionBar();
        CursorLayer.Visibility = Visibility.Collapsed;
    }

    private SelectionGrip HitTestGrip(Point point)
    {
        if (_selection.IsEmpty)
        {
            return SelectionGrip.None;
        }

        var rect = GetDipRect();
        var horizontal = SelectionGrip.None;
        if (point.Y >= rect.Top - ResizeHitThickness && point.Y <= rect.Bottom + ResizeHitThickness)
        {
            var toLeft = Math.Abs(point.X - rect.Left);
            var toRight = Math.Abs(point.X - rect.Right);
            if (toLeft <= ResizeHitThickness && toLeft <= toRight)
            {
                horizontal = SelectionGrip.Left;
            }
            else if (toRight <= ResizeHitThickness)
            {
                horizontal = SelectionGrip.Right;
            }
        }

        var vertical = SelectionGrip.None;
        if (point.X >= rect.Left - ResizeHitThickness && point.X <= rect.Right + ResizeHitThickness)
        {
            var toTop = Math.Abs(point.Y - rect.Top);
            var toBottom = Math.Abs(point.Y - rect.Bottom);
            if (toTop <= ResizeHitThickness && toTop <= toBottom)
            {
                vertical = SelectionGrip.Top;
            }
            else if (toBottom <= ResizeHitThickness)
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
        _ => Cursors.Arrow,
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
}

