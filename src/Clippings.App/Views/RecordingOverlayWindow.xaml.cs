using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Clippings.App.Strings;
using Clippings.Core.Editing;
using Clippings.Core.Input;
using Clippings.Platform.Interop;

namespace Clippings.App.Views;

public partial class RecordingOverlayWindow : Window
{
    private const int PauseResumeHotkeyId = 0x5201;
    private const int StopHotkeyId = 0x5202;

    private static readonly Geometry PauseIcon = Geometry.Parse("M2,1 L5,1 L5,13 L2,13 Z M9,1 L12,1 L12,13 L9,13 Z");
    private static readonly Geometry PlayIcon = Geometry.Parse("M3,1 L13,7 L3,13 Z");

    private readonly RecordingTarget _target;
    private readonly Func<TimeSpan> _elapsedProvider;
    private readonly Func<Task> _pauseAsync;
    private readonly Func<Task> _resumeAsync;
    private readonly Func<Task> _stopAsync;
    private readonly DispatcherTimer _timer;
    private ToolTip? _pauseToolTip;
    private ToolTip? _stopToolTip;
    private HwndSource? _hotkeySource;
    private IntPtr _hotkeyHandle;
    private bool _pauseHotkeyRegistered;
    private bool _stopHotkeyRegistered;
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
        InitializeToolTips();
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

    private void InitializeToolTips()
    {
        _pauseToolTip = new ToolTip();
        _pauseToolTip.Opened += OnToolTipOpened;
        PauseResumeButton.ToolTip = _pauseToolTip;

        _stopToolTip = new ToolTip();
        _stopToolTip.Opened += OnToolTipOpened;
        StopButton.ToolTip = _stopToolTip;
    }

    private void UpdatePausedVisual()
    {
        PauseResumeIcon.Data = _paused ? PlayIcon : PauseIcon;
        var text = _paused ? AppStrings.ResumeRecording : AppStrings.PauseRecording;
        if (_pauseHotkeyRegistered)
        {
            text = $"{text} ({RecordingHotkeys.PauseResume.DisplayName})";
        }

        if (_pauseToolTip is not null)
        {
            _pauseToolTip.Content = text;
        }

        AutomationProperties.SetName(PauseResumeButton, text);
        StateText.Text = _paused ? AppStrings.RecordingPausedStatus : "錄影中";
    }

    private void UpdateStopToolTip()
    {
        var text = AppStrings.StopRecording;
        if (_stopHotkeyRegistered)
        {
            text = $"{text} ({RecordingHotkeys.Stop.DisplayName})";
        }

        if (_stopToolTip is not null)
        {
            _stopToolTip.Content = text;
        }

        AutomationProperties.SetName(StopButton, text);
    }

    private void OnToolTipOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is ToolTip toolTip)
        {
            ExcludeToolTipFromCapture(toolTip, retriesRemaining: 2);
        }
    }

    private void ExcludeToolTipFromCapture(ToolTip toolTip, int retriesRemaining)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!toolTip.IsVisible)
            {
                return;
            }

            if (PresentationSource.FromVisual(toolTip) is HwndSource source && source.Handle != IntPtr.Zero)
            {
                _ = WindowHelper.ExcludeFromCapture(source.Handle);
                return;
            }

            if (retriesRemaining > 0)
            {
                ExcludeToolTipFromCapture(toolTip, retriesRemaining - 1);
            }
        });
    }

    private async void OnPauseResumeClick(object sender, RoutedEventArgs e)
        => await TogglePauseAsync();

    private async Task TogglePauseAsync()
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

    private async void OnStopClick(object sender, RoutedEventArgs e) => await StopRecordingAsync();

    private async Task StopRecordingAsync()
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
        _hotkeyHandle = new WindowInteropHelper(this).Handle;
        _ = WindowHelper.ExcludeFromCapture(_hotkeyHandle);
        WindowHelper.SetOverlayWindowStyles(_hotkeyHandle);

        _hotkeySource = HwndSource.FromHwnd(_hotkeyHandle);
        if (_hotkeySource is not null)
        {
            _hotkeySource.AddHook(OnHotkeyMessage);
            _pauseHotkeyRegistered = NativeMethods.RegisterHotKey(
                _hotkeyHandle,
                PauseResumeHotkeyId,
                (uint)RecordingHotkeys.PauseResume.Modifiers | NativeMethods.ModNoRepeat,
                RecordingHotkeys.PauseResume.VirtualKey);
            _stopHotkeyRegistered = NativeMethods.RegisterHotKey(
                _hotkeyHandle,
                StopHotkeyId,
                (uint)RecordingHotkeys.Stop.Modifiers | NativeMethods.ModNoRepeat,
                RecordingHotkeys.Stop.VirtualKey);
        }

        UpdatePausedVisual();
        UpdateStopToolTip();
    }

    private IntPtr OnHotkeyMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != NativeMethods.WmHotkey)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case PauseResumeHotkeyId:
                handled = true;
                _ = TogglePauseAsync();
                break;
            case StopHotkeyId:
                handled = true;
                _ = StopRecordingAsync();
                break;
        }

        return IntPtr.Zero;
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

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_pauseToolTip is not null)
        {
            _pauseToolTip.Opened -= OnToolTipOpened;
            _pauseToolTip = null;
        }

        if (_stopToolTip is not null)
        {
            _stopToolTip.Opened -= OnToolTipOpened;
            _stopToolTip = null;
        }

        _hotkeySource?.RemoveHook(OnHotkeyMessage);
        _hotkeySource = null;

        if (_hotkeyHandle != IntPtr.Zero)
        {
            if (_pauseHotkeyRegistered)
            {
                _ = NativeMethods.UnregisterHotKey(_hotkeyHandle, PauseResumeHotkeyId);
            }

            if (_stopHotkeyRegistered)
            {
                _ = NativeMethods.UnregisterHotKey(_hotkeyHandle, StopHotkeyId);
            }
        }

        _pauseHotkeyRegistered = false;
        _stopHotkeyRegistered = false;
        _hotkeyHandle = IntPtr.Zero;
    }

    private static string FormatElapsed(TimeSpan elapsed)
        => $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
}
