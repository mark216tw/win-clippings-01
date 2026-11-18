using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Clippings.App.Helpers;
using Clippings.App.Models;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Models;
using Clippings.Platform.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Clippings.App.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly AppState _state;
    private readonly CaptureCoordinator _coordinator;
    private readonly RecordingCoordinator _recording;
    private readonly ScreenAnnotationService _annotations;
    private readonly DispatcherTimer _recordingTimer;

    private CaptureWorkspacePage? _workspacePage;
    private SettingsPage? _settingsPage;
    private bool _loadedOnce;
    private bool _suppressNavigation;
    private int _currentPageIndex;

    public MainWindow(
        IServiceProvider services,
        AppState state,
        CaptureCoordinator coordinator,
        RecordingCoordinator recording,
        ScreenAnnotationService annotations)
    {
        InitializeComponent();
        WindowSizingHelper.ClampToWorkArea(this);

        _services = services;
        _state = state;
        _coordinator = coordinator;
        _recording = recording;
        _annotations = annotations;
        _recording.StateChanged += OnRecordingStateChanged;
        _annotations.SessionChanged += OnAnnotationSessionChanged;
        _recordingTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, OnRecordingTimerTick, Dispatcher);
        _recordingTimer.Stop();

        RefreshSettingsSummary();
        UpdateRecordingControls();
        UpdateScreenAnnotationButton();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        WindowHelper.ApplyDarkTitleBar(handle, App.IsDarkTheme);
        WindowHelper.ApplyRoundedCorners(handle);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loadedOnce)
        {
            return;
        }

        _loadedOnce = true;
        var index = NavList.SelectedIndex < 0 ? 0 : NavList.SelectedIndex;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => NavigateTo(index)));
    }

    public void NavigateTo(int index)
    {
        var safeIndex = Math.Clamp(index, 0, 1);
        if (!CanNavigateTo(safeIndex))
        {
            RestoreNavigationSelection();
            return;
        }

        if (NavList.SelectedIndex != safeIndex)
        {
            NavList.SelectedIndex = safeIndex;
        }

        ShowPage(safeIndex);
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressNavigation)
        {
            return;
        }

        if (NavList.SelectedIndex >= 0)
        {
            var target = NavList.SelectedIndex;
            if (!CanNavigateTo(target))
            {
                RestoreNavigationSelection();
                return;
            }

            ShowPage(target);
        }
    }

    private bool CanNavigateTo(int target)
        => target == _currentPageIndex || _currentPageIndex != 1 || _settingsPage?.ConfirmLeave() != false;

    private void RestoreNavigationSelection()
    {
        _suppressNavigation = true;
        NavList.SelectedIndex = _currentPageIndex;
        _suppressNavigation = false;
    }

    private void ShowPage(int index)
    {
        if (_currentPageIndex == 1 && index != 1)
        {
            _settingsPage?.StopHotkeyRecording();
        }

        switch (index)
        {
            case 0:
                _workspacePage ??= _services.GetRequiredService<CaptureWorkspacePage>();
                ContentHost.Content = _workspacePage;
                _ = _workspacePage.ActivateAsync();
                break;
            default:
                if (_settingsPage is null)
                {
                    _settingsPage = _services.GetRequiredService<SettingsPage>();
                    _settingsPage.LoadSettings();
                }

                ContentHost.Content = _settingsPage;
                break;
        }

        _currentPageIndex = index;
    }

    public async Task ShowWorkspaceTabAsync(WorkspaceTab tab)
    {
        NavigateTo(0);
        if (_workspacePage is not null)
        {
            await _workspacePage.ShowTabAsync(tab);
        }
    }

    public async Task FlushWorkspaceAsync()
    {
        if (_workspacePage is not null)
        {
            await _workspacePage.FlushPendingAsync();
        }
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        _ = ((sender as FrameworkElement)?.Tag as string) switch
        {
            "FullScreen" => _coordinator.CaptureFullScreenAsync(CaptureInvocationSource.QuickCapture),
            "Monitor" => _coordinator.CaptureMonitorAsync(CaptureInvocationSource.QuickCapture),
            "Window" => _coordinator.CaptureWindowAsync(CaptureInvocationSource.QuickCapture),
            "Delayed" => _coordinator.CaptureDelayedAsync(CaptureInvocationSource.QuickCapture),
            _ => _coordinator.CaptureRegionAsync(CaptureInvocationSource.QuickCapture),
        };
    }

    private void OnScreenAnnotationClick(object sender, RoutedEventArgs e)
        => _annotations.Toggle();

    private void OnAnnotationSessionChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateScreenAnnotationButton();
        }
        else
        {
            _ = Dispatcher.BeginInvoke(UpdateScreenAnnotationButton);
        }
    }

    private void UpdateScreenAnnotationButton()
    {
        var active = _annotations.IsActive;
        ScreenAnnotationButton.Content = active ? AppStrings.StopScreenAnnotation : AppStrings.ScreenAnnotation;
        ScreenAnnotationButton.ToolTip = active ? AppStrings.StopScreenAnnotationToolTip : AppStrings.ScreenAnnotationToolTip;
        AutomationProperties.SetName(
            ScreenAnnotationButton,
            active ? AppStrings.StopScreenAnnotation : AppStrings.ScreenAnnotation);
    }

    private void OnSelectWindowRecordingClick(object sender, RoutedEventArgs e)
        => _ = _recording.StartWithSelectionAsync();

    private void OnFullScreenRecordingClick(object sender, RoutedEventArgs e)
        => _ = _recording.StartFullScreenAsync();

    private void OnRecordingActionClick(object sender, RoutedEventArgs e)
    {
        _ = _recording.State == RecordingState.Idle
            ? _recording.StartWithSelectionAsync()
            : _recording.StopAsync();
    }

    private void OnOpenRecordingClick(object sender, RoutedEventArgs e)
        => _ = _recording.OpenLastRecording();

    private void OnOpenRecordingFolderClick(object sender, RoutedEventArgs e)
        => _ = _recording.OpenRecordingFolder();

    private void OnRecordingStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateRecordingControls();
        }
        else
        {
            _ = Dispatcher.BeginInvoke(UpdateRecordingControls);
        }
    }

    private void OnRecordingTimerTick(object? sender, EventArgs e)
        => UpdateRecordingControls();

    private void UpdateRecordingControls()
    {
        var state = _recording.State;
        var idle = state == RecordingState.Idle;
        SelectWindowRecordingButton.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        FullScreenRecordingButton.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        SelectWindowRecordingButton.IsEnabled = idle && _recording.CanStartRecording;
        FullScreenRecordingButton.IsEnabled = idle && _recording.CanStartRecording;
        RecordingActionButton.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;

        RecordingActionButton.Content = state switch
        {
            RecordingState.Countdown => "取消倒數",
            RecordingState.Starting => "取消準備",
            RecordingState.Recording => "停止錄影",
            RecordingState.Paused => "停止錄影",
            RecordingState.Stopping => "正在完成…",
            _ => "停止錄影",
        };
        RecordingActionButton.IsEnabled = state != RecordingState.Stopping;

        RecordingStatusText.Text = state switch
        {
            RecordingState.Countdown => $"將在 {_recording.RemainingDelaySeconds ?? 0} 秒後開始…",
            RecordingState.Starting => "正在準備 H.264 編碼器…",
            RecordingState.Recording => $"錄影中  {FormatElapsedTime()}",
            RecordingState.Paused => $"{AppStrings.RecordingPausedStatus}  {FormatElapsedTime()}",
            RecordingState.Stopping => "正在完成 MP4，請稍候…",
            _ when _recording.IsPreparing => "正在準備錄影…",
            _ when _recording.LastOutputPath is not null => "上次錄影已完成",
            _ => "尚未錄影",
        };

        if (state is RecordingState.Recording or RecordingState.Paused)
        {
            _recordingTimer.Start();
        }
        else
        {
            _recordingTimer.Stop();
        }

        OpenRecordingButton.IsEnabled = _recording.LastOutputPath is { } path && File.Exists(path);
        OpenRecordingFolderButton.IsEnabled = idle;
    }

    private string FormatElapsedTime()
        => $"{(int)_recording.Elapsed.TotalHours:00}:{_recording.Elapsed.Minutes:00}:{_recording.Elapsed.Seconds:00}";

    public void RefreshSettingsSummary()
    {
        var settings = _state.Settings;

        RegionCaptureButton.ToolTip = $"{AppStrings.CaptureRegion} ({settings.HotkeyRegion})";
        FullScreenCaptureButton.ToolTip = $"{AppStrings.CaptureFullScreen} ({settings.HotkeyFullScreen})";
        MonitorCaptureButton.ToolTip = "目前螢幕擷取";
        WindowCaptureButton.ToolTip = $"{AppStrings.CaptureWindow} ({settings.HotkeyWindow})";
        DelayedCaptureButton.ToolTip = $"{AppStrings.CaptureDelayed} ({settings.HotkeyDelayed})";
    }

    public void UpdateHotkeyStatus(IReadOnlyList<string> failures)
    {
        HotkeyWarning.Text = failures.Count == 0
            ? string.Empty
            : "無法啟用：" + string.Join("、", failures);
        HotkeyWarning.Visibility = failures.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_currentPageIndex == 0 && _workspacePage?.HandleEditorKey(e) == true)
        {
            e.Handled = true;
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!App.Current.IsExiting)
        {
            e.Cancel = true;
            if (_settingsPage?.ConfirmLeave() == false)
            {
                return;
            }

            _settingsPage?.StopHotkeyRecording();
            _ = FlushWorkspaceAsync();
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _recording.StateChanged -= OnRecordingStateChanged;
        _annotations.SessionChanged -= OnAnnotationSessionChanged;
        _recordingTimer.Stop();
        _settingsPage?.StopHotkeyRecording();
        _workspacePage = null;
        _settingsPage = null;
    }
}




