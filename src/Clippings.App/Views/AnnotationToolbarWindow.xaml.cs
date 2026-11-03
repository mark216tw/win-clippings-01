using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Clippings.App.Services;
using Clippings.Core.Models;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using SkiaSharp;

namespace Clippings.App.Views;

public partial class AnnotationToolbarWindow : Window
{
    private static readonly string[] BackdropNames = ["跟隨畫面", "白板", "黑板"];

    private readonly ScreenAnnotationService _service;
    private readonly MonitorService _monitors;
    private bool _colorPopupClosedByLightDismiss;
    private bool _updating;
    private bool _ready;

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
            BackdropCombo.ItemsSource = BackdropNames;

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
        PositionNearCursor();
        SyncFromService();
        Activate();
        Focus();
    }

    public IntPtr Handle { get; private set; }

    private void OnClosed(object? sender, EventArgs e)
    {
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

            BackdropCombo.SelectedIndex = (int)_service.Backdrop;
            PointerButton.IsChecked = _service.ClickThrough;
            EyeVisiblePath.Visibility = _service.StrokesVisible ? Visibility.Visible : Visibility.Collapsed;
            EyeHiddenPath.Visibility = _service.StrokesVisible ? Visibility.Collapsed : Visibility.Visible;
            VisibilityButton.ToolTip = _service.StrokesVisible ? "隱藏所有標註（9、V）" : "顯示所有標註（9、V）";
            UpdateUndoButtons();
        }
        finally
        {
            _updating = false;
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

    private void OnBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            Mouse.LeftButton != MouseButtonState.Pressed ||
            IsInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        e.Handled = true;
        DragMove();
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
    }

    private void OnColorPopupClosed(object? sender, EventArgs e)
    {
        _colorPopupClosedByLightDismiss = true;
        Dispatcher.BeginInvoke(() => _colorPopupClosedByLightDismiss = false);
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

    private void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || !_ready || BackdropCombo.SelectedIndex < 0)
        {
            return;
        }

        _service.SetBackdrop((AnnotationBackdrop)BackdropCombo.SelectedIndex);
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
        DragMove();
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
