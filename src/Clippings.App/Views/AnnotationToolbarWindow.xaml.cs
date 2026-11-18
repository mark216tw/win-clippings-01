using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Clippings.App.Services;
using Clippings.Core.Editing;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using Microsoft.Win32;
using SkiaSharp;

namespace Clippings.App.Views;

public partial class AnnotationToolbarWindow : Window
{
    private static readonly string[] BackdropNames = ["透明", "白板", "黑板"];

    private const double SnapThresholdDip = 16;
    private const double HandleThickness = 12;
    private const double HandleLength = 96;

    private readonly ScreenAnnotationService _service;
    private readonly MonitorService _monitors;
    private readonly DispatcherTimer _autoHideTimer;
    private bool _colorPopupClosedByLightDismiss;
    private bool _updating;
    private bool _ready;
    private bool _hasLoaded;
    private bool _collapsed;
    private bool _dragging;
    private bool _captureSuspended;
    private AnnotationDockEdge _dockEdge = AnnotationDockEdge.None;
    private PixelRect _dockWorkArea = PixelRect.Empty;
    private double _dockDpi = 96;
    private double _dockOffsetRatio = 0.5;
    private double _expandedWidthDip;
    private double _expandedHeightDip;

    public AnnotationToolbarWindow(ScreenAnnotationService service, MonitorService monitors)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(monitors);

        _service = service;
        _monitors = monitors;

        try
        {
            InitializeComponent();

            StrokeCombo.Items.Add(2);
            StrokeCombo.Items.Add(4);
            StrokeCombo.Items.Add(6);
            StrokeCombo.Items.Add(8);
            StrokeCombo.Items.Add(12);

            _autoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_service.AutoHideSeconds) };
            _autoHideTimer.Tick += OnAutoHideTick;

            _service.ToolStateChanged += OnToolStateChanged;
            _service.Document.Changed += OnDocumentChanged;
            SyncFromService();
            _ready = true;
        }
        catch
        {
            _service.ToolStateChanged -= OnToolStateChanged;
            _service.Document.Changed -= OnDocumentChanged;
            throw;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Handle = new WindowInteropHelper(this).Handle;
        _hasLoaded = true;

        var restored = false;
        if (_service.DockEdge != AnnotationDockEdge.None)
        {
            var (cursorX, cursorY) = WindowHelper.GetCursorPosition();
            var monitor = _monitors.GetMonitorAt(cursorX, cursorY) ?? _monitors.GetPrimaryMonitor();
            if (monitor is not null)
            {
                _dockEdge = _service.DockEdge;
                _dockWorkArea = monitor.WorkArea;
                _dockDpi = monitor.Dpi;
                _dockOffsetRatio = AnnotationDockHelper.NormalizeRatio(_service.DockOffsetRatio);
                restored = true;
                ApplyDockLayout();
            }
        }

        if (!restored)
        {
            PositionNearCursor();
            if (_service.DockEdge != AnnotationDockEdge.None)
            {
                _service.UpdateDockState(AnnotationDockEdge.None, _dockOffsetRatio);
            }
        }

        SyncFromService();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (restored)
        {
            StartAutoHideTimer();
        }

        Activate();
        Focus();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (_hasLoaded)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(RefreshDockLayout));
        }
    }

    public IntPtr Handle { get; private set; }

    private void OnClosed(object? sender, EventArgs e)
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _autoHideTimer.Stop();
        _service.ToolStateChanged -= OnToolStateChanged;
        _service.Document.Changed -= OnDocumentChanged;
    }

    private void PositionNearCursor()
    {
        var (cursorX, cursorY) = WindowHelper.GetCursorPosition();
        var monitor = _monitors.GetMonitorAt(cursorX, cursorY) ?? _monitors.GetMonitors().FirstOrDefault();
        if (monitor is null)
        {
            return;
        }

        var scale = 96.0 / monitor.Dpi;
        var width = ActualWidth > 1 ? ActualWidth : Width;
        var left = monitor.Bounds.X * scale + Math.Max(8, ((monitor.Bounds.Width * scale) - width) / 2);
        Left = left;
        Top = monitor.Bounds.Y * scale + 24;
    }

    private void SyncFromService()
    {
        _updating = true;
        try
        {
            LaserButton.IsChecked = _service.Tool == AnnotationTool.Laser;
            PenButton.IsChecked = _service.Tool == AnnotationTool.Pen;
            HighlighterButton.IsChecked = _service.Tool == AnnotationTool.Highlighter;
            LineButton.IsChecked = _service.Tool == AnnotationTool.Line;
            ArrowButton.IsChecked = _service.Tool == AnnotationTool.Arrow;
            RectangleButton.IsChecked = _service.Tool == AnnotationTool.Rectangle;
            EllipseButton.IsChecked = _service.Tool == AnnotationTool.Ellipse;
            EraserButton.IsChecked = _service.Tool == AnnotationTool.Eraser;
            MagnifierButton.IsChecked = _service.Tool == AnnotationTool.Magnifier;

            var color = Color.FromRgb(_service.Color.Red, _service.Color.Green, _service.Color.Blue);
            var brush = new SolidColorBrush(color);
            ColorMenuButton.Background = brush;
            PopupCurrentColorSwatch.Fill = brush;

            var hex = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            foreach (var swatch in SwatchGrid.Children.OfType<RadioButton>())
            {
                swatch.IsChecked = string.Equals(swatch.Tag as string, hex, StringComparison.OrdinalIgnoreCase);
            }

            StrokeCombo.SelectedItem = _service.StrokeWidth >= 11f ? 12
                : _service.StrokeWidth >= 7f ? 8
                : _service.StrokeWidth >= 5f ? 6
                : _service.StrokeWidth >= 3f ? 4
                : 2;

            var backdropIndex = Math.Clamp((int)_service.Backdrop, 0, BackdropNames.Length - 1);
            BackdropButton.Content = BackdropNames[backdropIndex];
            BackdropButton.ToolTip = $"標註背景：{BackdropNames[backdropIndex]}（點擊切換）";
            PointerButton.IsChecked = _service.ClickThrough;
            PinButton.IsChecked = !_service.AutoHideWhenSnapped;
            UpdateAutoHideInterval();
            EyeVisiblePath.Visibility = _service.StrokesVisible ? Visibility.Visible : Visibility.Collapsed;
            EyeHiddenPath.Visibility = _service.StrokesVisible ? Visibility.Collapsed : Visibility.Visible;
            VisibilityButton.ToolTip = _service.StrokesVisible ? "隱藏所有標註（9、V）" : "顯示所有標註（9、V）";
            UpdateDockUi();
            UpdateUndoButtons();
        }
        finally
        {
            _updating = false;
        }
    }

    private void UpdateDockUi()
    {
        var docked = _dockEdge != AnnotationDockEdge.None;
        PinButton.IsEnabled = docked;
        PinButton.ToolTip = !docked
            ? "拖曳工具列到螢幕上緣即可吸附；釘住時不會自動收合"
            : _service.AutoHideWhenSnapped
                ? $"未釘住：吸附後 {_service.AutoHideSeconds} 秒自動收合成邊緣把手"
                : "已釘住：不會自動收合";
    }

    private void UpdateAutoHideInterval()
    {
        var interval = TimeSpan.FromSeconds(_service.AutoHideSeconds);
        if (_autoHideTimer.Interval == interval)
        {
            return;
        }

        var wasEnabled = _autoHideTimer.IsEnabled;
        _autoHideTimer.Interval = interval;
        if (wasEnabled)
        {
            _autoHideTimer.Start();
        }
    }

    private void UpdateUndoButtons()
    {
        if (UndoButton is null || RedoButton is null)
        {
            return;
        }

        UndoButton.IsEnabled = _service.Document.CanUndo;
        RedoButton.IsEnabled = _service.Document.CanRedo;
    }

    private void OnToolClick(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready || sender is not RadioButton { Tag: string tag } ||
            !Enum.TryParse<AnnotationTool>(tag, out var tool))
        {
            return;
        }

        _service.SetTool(tool);
    }

    private void OnVisibilityClick(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        _service.ToggleStrokesVisible();
    }

    private void OnPinChecked(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _service.SetAutoHideWhenSnapped(false);
        _autoHideTimer.Stop();
        UpdateDockUi();
        if (_collapsed)
        {
            Expand(restartTimer: false);
        }
    }

    private void OnPinUnchecked(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        _service.SetAutoHideWhenSnapped(true);
        UpdateDockUi();
        StartAutoHideTimer();
    }

    private void OnWindowMouseEnter(object sender, MouseEventArgs e)
    {
        _autoHideTimer.Stop();
        if (_collapsed)
        {
            Expand(restartTimer: false);
        }
    }

    private void OnWindowMouseLeave(object sender, MouseEventArgs e) => StartAutoHideTimer();

    private void OnAutoHideTick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();
        Collapse();
    }

    private void StartAutoHideTimer()
    {
        _autoHideTimer.Stop();

        if (!_hasLoaded || _collapsed || _dragging || _captureSuspended ||
            _dockEdge == AnnotationDockEdge.None || !_service.AutoHideWhenSnapped ||
            IsMouseOver || ColorPopup.IsOpen)
        {
            return;
        }

        _autoHideTimer.Start();
    }

    private void Collapse()
    {
        if (!_hasLoaded || _collapsed || _dragging || _captureSuspended ||
            _dockEdge == AnnotationDockEdge.None || !_service.AutoHideWhenSnapped ||
            IsMouseOver || ColorPopup.IsOpen)
        {
            return;
        }

        _autoHideTimer.Stop();
        _expandedWidthDip = ActualWidth;
        _expandedHeightDip = ActualHeight;
        _collapsed = true;
        ConfigureHandle();
        BarRoot.Visibility = Visibility.Collapsed;
        HandleRoot.Visibility = Visibility.Visible;
        UpdateLayout();
        ApplyDockLayout();
    }

    private void Expand(bool restartTimer)
    {
        if (!_collapsed)
        {
            if (restartTimer)
            {
                StartAutoHideTimer();
            }

            return;
        }

        _collapsed = false;
        HandleRoot.Visibility = Visibility.Collapsed;
        BarRoot.Visibility = Visibility.Visible;
        UpdateLayout();
        ApplyDockLayout();

        if (restartTimer)
        {
            StartAutoHideTimer();
        }
    }

    private void ConfigureHandle()
    {
        HandleRoot.Width = HandleLength;
        HandleRoot.Height = HandleThickness;
        HandleRoot.CornerRadius = new CornerRadius(0, 0, 8, 8);
        HandleChevron.RenderTransform = new RotateTransform(0);
    }

    private void ApplyDockLayout()
    {
        if (!IsVisible || _dockEdge == AnnotationDockEdge.None || _dockWorkArea.IsEmpty || Handle == IntPtr.Zero)
        {
            return;
        }

        var scale = _dockDpi / 96.0;
        var (widthDip, heightDip) = GetTargetSizeDip();
        var width = (int)Math.Round(widthDip * scale);
        var height = (int)Math.Round(heightDip * scale);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var bounds = AnnotationDockHelper.ComputeBounds(
            _dockWorkArea,
            width,
            height,
            _dockEdge,
            _dockOffsetRatio);
        if (bounds.IsEmpty)
        {
            return;
        }

        Left = bounds.X * 96.0 / _dockDpi;
        Top = bounds.Y * 96.0 / _dockDpi;
        WindowHelper.SetPhysicalBounds(Handle, bounds);
        _service.EnsureToolbarOnTop();
    }

    private (double Width, double Height) GetTargetSizeDip()
    {
        if (_collapsed)
        {
            return (HandleLength, HandleThickness);
        }

        return _expandedWidthDip > 1 && _expandedHeightDip > 1
            ? (_expandedWidthDip, _expandedHeightDip)
            : (ActualWidth, ActualHeight);
    }

    private void ApplySnapAfterDrag()
    {
        if (!TryGetWindowBounds(out var window))
        {
            return;
        }

        var monitor = _monitors.GetMonitorForRect(window) ?? _monitors.GetPrimaryMonitor();
        if (monitor is null)
        {
            ClearDock();
            return;
        }

        var scale = monitor.Dpi / 96.0;
        var threshold = (int)Math.Round(SnapThresholdDip * scale);
        var edge = AnnotationDockHelper.DetectEdge(window, monitor.WorkArea, threshold);
        if (edge == AnnotationDockEdge.None)
        {
            ClearDock();
            return;
        }

        _dockEdge = edge;
        _dockWorkArea = monitor.WorkArea;
        _dockDpi = monitor.Dpi;
        _dockOffsetRatio = AnnotationDockHelper.ComputeOffsetRatio(window, monitor.WorkArea, edge);
        _service.UpdateDockState(_dockEdge, _dockOffsetRatio);
        UpdateDockUi();
        ApplyDockLayout();
        StartAutoHideTimer();
    }

    private void ClearDock()
    {
        _autoHideTimer.Stop();
        _dockEdge = AnnotationDockEdge.None;
        _dockWorkArea = PixelRect.Empty;
        _service.UpdateDockState(AnnotationDockEdge.None, _dockOffsetRatio);
        UpdateDockUi();

        if (_collapsed)
        {
            Expand(restartTimer: false);
        }
    }

    private bool TryGetWindowBounds(out PixelRect bounds)
    {
        bounds = PixelRect.Empty;
        if (Handle == IntPtr.Zero || !NativeMethods.GetWindowRect(Handle, out var rect))
        {
            return false;
        }

        bounds = new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return !bounds.IsEmpty;
    }

    public void RefreshDockLayout()
    {
        if (!_hasLoaded)
        {
            return;
        }

        _autoHideTimer.Stop();

        if (_dockEdge != AnnotationDockEdge.None)
        {
            _ = TryGetWindowBounds(out var window);
            var monitor = _monitors.GetMonitorForRect(window) ?? _monitors.GetPrimaryMonitor();
            if (monitor is null)
            {
                ClearDock();
                return;
            }

            _dockWorkArea = monitor.WorkArea;
            _dockDpi = monitor.Dpi;
            ApplyDockLayout();
        }

        StartAutoHideTimer();
    }

    public void SuspendAutoHide()
    {
        _captureSuspended = true;
        _autoHideTimer.Stop();
    }

    public void ResumeAutoHide()
    {
        _captureSuspended = false;
        if (_collapsed)
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(ApplyDockLayout));
        }

        StartAutoHideTimer();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshDockLayout));

    private void OnBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            Mouse.LeftButton != MouseButtonState.Pressed ||
            IsInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        BeginDrag();
        try
        {
            DragMove();
        }
        finally
        {
            EndDrag();
        }
    }

    private void OnHandleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        e.Handled = true;
        Expand(restartTimer: true);
        Activate();
        Focus();
    }

    private void BeginDrag()
    {
        _dragging = true;
        _autoHideTimer.Stop();
        if (_collapsed)
        {
            Expand(restartTimer: false);
        }
        else
        {
            _expandedWidthDip = ActualWidth;
            _expandedHeightDip = ActualHeight;
        }
    }

    private void EndDrag()
    {
        _dragging = false;
        ApplySnapAfterDrag();
    }

    private static bool IsInteractiveControl(DependencyObject? source)
    {
        for (DependencyObject? current = source;
             current is not null;
             current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase or ComboBox or Popup)
            {
                return true;
            }
        }

        return false;
    }

    private void OnColorMenuClick(object sender, RoutedEventArgs e)
    {
        if (_colorPopupClosedByLightDismiss)
        {
            return;
        }

        ColorPopup.IsOpen = !ColorPopup.IsOpen;
        if (ColorPopup.IsOpen)
        {
            _autoHideTimer.Stop();
        }
    }

    private void OnColorPopupClosed(object? sender, EventArgs e)
    {
        _colorPopupClosedByLightDismiss = true;
        Dispatcher.BeginInvoke(() => _colorPopupClosedByLightDismiss = false);
        StartAutoHideTimer();
    }

    private void OnColorChecked(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready || sender is not RadioButton { IsChecked: true, Tag: string hex } ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            return;
        }

        _service.SetColor(new SKColor(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF),
            255));
        SyncFromService();
        ColorPopup.IsOpen = false;
    }

    private void OnStrokeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || !_ready || StrokeCombo.SelectedItem is not int width)
        {
            return;
        }

        _service.SetStrokeWidth(width);
    }

    private void OnBackdropClick(object sender, RoutedEventArgs e)
    {
        if (_updating || !_ready)
        {
            return;
        }

        var next = (AnnotationBackdrop)(((int)_service.Backdrop + 1) % BackdropNames.Length);
        _service.SetBackdrop(next);
    }

    private void OnPointerChecked(object sender, RoutedEventArgs e)
    {
        if (!_updating && _ready)
        {
            _service.SetClickThrough(true);
        }
    }

    private void OnPointerUnchecked(object sender, RoutedEventArgs e)
    {
        if (!_updating && _ready)
        {
            _service.SetClickThrough(false);
        }
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        _service.Document.Undo();
        UpdateUndoButtons();
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        _service.Document.Redo();
        UpdateUndoButtons();
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        _service.Document.ClearAll();
        UpdateUndoButtons();
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e) => _service.RequestCapture();

    private void OnExitClick(object sender, RoutedEventArgs e) => _service.Stop();

    private void OnDragGripDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Mouse.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        e.Handled = true;
        BeginDrag();
        try
        {
            DragMove();
        }
        finally
        {
            EndDrag();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z:
                    _service.Document.Undo();
                    UpdateUndoButtons();
                    e.Handled = true;
                    return;
                case Key.Y:
                    _service.Document.Redo();
                    UpdateUndoButtons();
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

        if (IsInsideInputControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (_service.TryHandleShortcut(e.Key))
        {
            e.Handled = true;
        }
    }

    private static bool IsInsideInputControl(DependencyObject? source)
    {
        for (DependencyObject? current = source;
             current is not null;
             current = LogicalTreeHelper.GetParent(current) ?? VisualTreeHelper.GetParent(current))
        {
            if (current is TextBox or ComboBox or Popup)
            {
                return true;
            }
        }

        return false;
    }

    private void OnToolStateChanged(object? sender, EventArgs e) => SyncFromService();

    private void OnDocumentChanged(object? sender, EventArgs e) => UpdateUndoButtons();
}
