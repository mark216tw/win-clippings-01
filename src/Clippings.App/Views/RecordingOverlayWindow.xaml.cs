using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clippings.Core.Editing;
using Clippings.Platform.Interop;

namespace Clippings.App.Views;

public partial class RecordingOverlayWindow : Window
{
    private static readonly Geometry PauseIcon = Geometry.Parse("M2,1 L5,1 L5,13 L2,13 Z M9,1 L12,1 L12,13 L9,13 Z");
    private static readonly Geometry PlayIcon = Geometry.Parse("M3,1 L13,7 L3,13 Z");

    private readonly RecordingTarget _target;
    private readonly Func<TimeSpan> _elapsedProvider;
    private readonly Func<Task> _pauseAsync;
    private readonly Func<Task> _resumeAsync;
    private readonly Func<Task> _stopAsync;
    private readonly DispatcherTimer _timer;
    private bool _paused;
    private bool _stopping;

    public RecordingOverlayWindow(
        RecordingTarget target,
        Func<TimeSpan> elapsedProvider,
        Func<Task> pauseAsync,
        Func<Task> resumeAsync,
        Func<Task> stopAsync)
    {
        _target = target;
        _elapsedProvider = elapsedProvider;
        _pauseAsync = pauseAsync;
        _resumeAsync = resumeAsync;
        _stopAsync = stopAsync;
        InitializeComponent();
        TargetText.Text = target.DisplayName;
        TargetText.ToolTip = target.DisplayName;
        if (target.IsWindow)
        {
            TargetText.Cursor = Cursors.Hand;
            TargetText.ToolTip = $"{target.DisplayName}{Environment.NewLine}點擊以還原並帶到最前方";
            AutomationProperties.SetName(TargetText, $"{target.DisplayName}，點擊以帶到最前方");
        }
        UpdatePausedVisual();
        UpdateElapsed();
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnTimerTick, Dispatcher);
        _timer.Start();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        UpdatePausedVisual();
        UpdateElapsed();
    }

    private void OnTimerTick(object? sender, EventArgs e) => UpdateElapsed();

    private void OnTargetTextMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_target.IsWindow)
        {
            return;
        }

        var target = new WindowCaptureTarget(_target.Handle, _target.ProcessId);
        if (WindowHelper.TryActivateWindow(target, restoreIfMinimized: true))
        {
            e.Handled = true;
        }
    }

    private void UpdateElapsed()
        => ElapsedText.Text = FormatElapsed(_elapsedProvider());

    private void UpdatePausedVisual()
    {
        PauseResumeIcon.Data = _paused ? PlayIcon : PauseIcon;
        var text = _paused ? "繼續錄影" : "暫停錄影";
        ToolTipService.SetToolTip(PauseResumeButton, text);
        AutomationProperties.SetName(PauseResumeButton, text);
        StateText.Text = _paused ? "已暫停" : "錄影中";
    }

    private async void OnPauseResumeClick(object sender, RoutedEventArgs e)
    {
        if (_stopping)
        {
            return;
        }

        try
        {
            if (_paused)
            {
                await _resumeAsync();
            }
            else
            {
                await _pauseAsync();
            }
        }
        catch (Exception)
        {
            StateText.Text = "操作失敗";
        }
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_stopping)
        {
            return;
        }

        _stopping = true;
        PauseResumeButton.IsEnabled = false;
        StopButton.IsEnabled = false;
        StateText.Text = "正在完成…";
        try
        {
            await _stopAsync();
        }
        catch (Exception)
        {
            StateText.Text = "停止失敗";
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _ = WindowHelper.ExcludeFromCapture(handle);
        WindowHelper.SetOverlayWindowStyles(handle);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, PositionWindow);

    private void OnDpiChanged(object sender, DpiChangedEventArgs e)
        => _ = Dispatcher.BeginInvoke(PositionWindow);

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
        {
            _ = Dispatcher.BeginInvoke(PositionWindow);
        }
    }

    private void PositionWindow()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || ActualWidth < 1 || ActualHeight < 1)
        {
            return;
        }

        var scale = _target.Monitor.ScaleFactor;
        var width = checked((int)Math.Round(ActualWidth * scale));
        var height = checked((int)Math.Round(ActualHeight * scale));
        var margin = checked((int)Math.Round(16 * scale));
        var work = _target.Monitor.WorkArea;
        var left = work.Right - width - margin;
        var top = work.Y + margin;
        WindowHelper.SetPhysicalBounds(handle, new PixelRect(left, top, width, height));
        Opacity = 1;
    }

    private void OnClosed(object? sender, EventArgs e) => _timer.Stop();

    private static string FormatElapsed(TimeSpan elapsed)
        => $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
}
