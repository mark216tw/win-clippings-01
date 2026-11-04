using System.Diagnostics;
using System.Windows;
using Clippings.App.Strings;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Platform.Capture;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Clippings.App.Services;

public sealed class RecordingCoordinator
{
    private const long MinimumFreeSpaceBytes = 256L * 1024 * 1024;
    private readonly object _sync = new();
    private readonly WindowsGraphicsRecordingService _recordingService;
    private readonly RecordingTargetPicker _targetPicker;
    private readonly RecordingCountdownService _countdown;
    private readonly RecordingOverlayService _overlay;
    private readonly MonitorService _monitors;
    private readonly INotificationService _notifications;
    private readonly ILogger<RecordingCoordinator> _logger;
    private readonly AppState _appState;
    private CancellationTokenSource? _stopSource;
    private Task? _recordingTask;
    private RecordingControl? _control;
    private RecordingTarget? _activeTarget;
    private RecordingState _state;
    private string? _lastOutputPath;
    private int? _remainingDelaySeconds;
    private bool _isPreparing;
    private bool _restoreMainWindow;

    public RecordingCoordinator(
        WindowsGraphicsRecordingService recordingService,
        RecordingTargetPicker targetPicker,
        RecordingCountdownService countdown,
        RecordingOverlayService overlay,
        MonitorService monitors,
        AppState appState,
        INotificationService notifications,
        ILogger<RecordingCoordinator> logger)
    {
        _recordingService = recordingService;
        _targetPicker = targetPicker;
        _countdown = countdown;
        _overlay = overlay;
        _monitors = monitors;
        _appState = appState;
        _notifications = notifications;
        _logger = logger;
        CleanupStaleTemporaryFiles();
    }

    public event EventHandler? StateChanged;

    public Window? MainWindow { get; set; }

    public RecordingState State
    {
        get
        {
            lock (_sync)
            {
                return _state;
            }
        }
    }

    public bool IsSupported => _recordingService.IsSupported;

    public TimeSpan Elapsed
    {
        get
        {
            RecordingControl? control;
            lock (_sync)
            {
                control = _control;
            }

            return control?.Elapsed ?? TimeSpan.Zero;
        }
    }

    public bool IsPaused => State == RecordingState.Paused;

    public string? LastOutputPath
    {
        get
        {
            lock (_sync)
            {
                return _lastOutputPath;
            }
        }
    }

    public int? RemainingDelaySeconds
    {
        get
        {
            lock (_sync)
            {
                return _remainingDelaySeconds;
            }
        }
    }

    public bool IsPreparing
    {
        get
        {
            lock (_sync)
            {
                return _isPreparing;
            }
        }
    }

    public bool CanStartRecording => State == RecordingState.Idle && IsSupported && !IsPreparing;

    public async Task StartWithSelectionAsync()
    {
        if (!TryBeginPreparing())
        {
            return;
        }

        await Task.Yield();
        if (!IsSupported)
        {
            FailPreparing(AppStrings.RecordingUnsupported);
            return;
        }

        WindowRecordingTarget? selected;
        try
        {
            selected = _targetPicker.PickTarget();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "顯示錄影視窗選擇器失敗。");
            FailPreparing(AppStrings.FormatRecordingFailed(exception.Message));
            return;
        }

        if (selected is null)
        {
            EndPreparing();
            return;
        }

        if (!TryCreateWindowTarget(selected, out var target))
        {
            FailPreparing(AppStrings.RecordingTargetUnavailable);
            return;
        }

        if (!WindowHelper.TryActivateWindow(selected.Target, restoreIfMinimized: true))
        {
            _logger.LogWarning("Windows 拒絕將錄影目標帶到最前方：{Handle}", target.Handle);
        }

        await StartRecordingAsync(target);
    }

    public async Task StartFullScreenAsync()
    {
        if (!TryBeginPreparing())
        {
            return;
        }

        await Task.Yield();
        if (!IsSupported)
        {
            FailPreparing(AppStrings.RecordingUnsupported);
            return;
        }

        if (!TryCreateFullScreenTarget(out var target))
        {
            FailPreparing(AppStrings.RecordingMonitorUnavailable);
            return;
        }

        await StartRecordingAsync(target);
    }

    public Task PauseAsync()
    {
        RecordingControl? control;
        lock (_sync)
        {
            if (_state != RecordingState.Recording)
            {
                return Task.CompletedTask;
            }

            _state = RecordingState.Paused;
            control = _control;
        }

        control?.Pause();
        _overlay.SetPaused(true);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        RecordingControl? control;
        lock (_sync)
        {
            if (_state != RecordingState.Paused)
            {
                return Task.CompletedTask;
            }

            _state = RecordingState.Recording;
            control = _control;
        }

        control?.Resume();
        _overlay.SetPaused(false);
        StateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? recordingTask;
        var changed = false;
        lock (_sync)
        {
            if (_state == RecordingState.Idle)
            {
                return;
            }

            if (_state != RecordingState.Stopping)
            {
                _state = RecordingState.Stopping;
                changed = true;
            }

            _stopSource?.Cancel();
            recordingTask = _recordingTask;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (recordingTask is not null)
        {
            await recordingTask.ConfigureAwait(false);
        }
    }

    public bool OpenLastRecording()
    {
        var path = LastOutputPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        return TryOpen(path);
    }

    public bool OpenRecordingFolder()
    {
        var directory = LastOutputPath is { } path && File.Exists(path)
            ? Path.GetDirectoryName(path)
            : GetConfiguredOutputDirectory();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return TryOpen(directory);
    }

    private bool TryBeginPreparing()
    {
        lock (_sync)
        {
            if (_state != RecordingState.Idle || _isPreparing)
            {
                return false;
            }

            _isPreparing = true;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void EndPreparing()
    {
        lock (_sync)
        {
            _isPreparing = false;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FailPreparing(string message)
    {
        _notifications.Show(message, isError: true);
        EndPreparing();
    }

    private bool TryCreateWindowTarget(WindowRecordingTarget selected, out RecordingTarget target)
    {
        target = default!;
        if (!WindowHelper.TryGetVisibleWindowBounds(selected.Target, out var bounds))
        {
            return false;
        }

        var monitor = _monitors.GetMonitorForRect(bounds) ?? _monitors.GetPrimaryMonitor();
        if (monitor is null)
        {
            return false;
        }

        target = new RecordingTarget(
            RecordingTargetKind.Window,
            selected.Target.Handle,
            selected.Target.ProcessId,
            selected.DisplayName,
            bounds,
            monitor);
        return true;
    }

    private bool TryCreateFullScreenTarget(out RecordingTarget target)
    {
        target = default!;
        var (cursorX, cursorY) = WindowHelper.GetCursorPosition();
        var monitor = _monitors.GetMonitorAt(cursorX, cursorY)
            ?? _monitors.GetPrimaryMonitor()
            ?? _monitors.GetMonitors().FirstOrDefault();
        if (monitor is null)
        {
            return false;
        }

        var deviceName = monitor.DeviceName.Replace(@"\\.\", string.Empty);
        target = new RecordingTarget(
            RecordingTargetKind.Monitor,
            monitor.Handle,
            0,
            $"{AppStrings.RecordingFullScreen}  -  {deviceName}",
            monitor.Bounds,
            monitor);
        return true;
    }

    private async Task StartRecordingAsync(RecordingTarget target)
    {
        CancellationTokenSource? stopSource = null;
        try
        {
            var settings = _appState.Settings.Recording.Clone();
            settings.FrameRate = settings.FrameRate is 30 or 60 ? settings.FrameRate : 30;
            settings.Bitrate = settings.Bitrate is 4_000_000 or 8_000_000 or 12_000_000 or 20_000_000
                ? settings.Bitrate
                : 8_000_000;
            settings.DelaySeconds = settings.DelaySeconds is 0 or 5 or 10 or 20 or 30
                ? settings.DelaySeconds
                : 0;
            settings.SystemAudioGain = settings.SystemAudioGain is 1.0 or 1.5 or 2.0 or 3.0
                ? settings.SystemAudioGain
                : 2.0;
            var outputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
                ? PathUtil.DefaultRecordingRoot
                : Path.GetFullPath(settings.OutputDirectory);
            EnsureRecordingDirectory(outputDirectory);
            var outputPath = CreateOutputPath(outputDirectory);
            stopSource = new CancellationTokenSource();
            var control = new RecordingControl();

            if (target.Kind == RecordingTargetKind.Monitor &&
                MainWindow is { IsVisible: true } mainWindow)
            {
                mainWindow.Hide();
                _restoreMainWindow = true;
                await Task.Delay(150);
            }

            lock (_sync)
            {
                if (_state != RecordingState.Idle)
                {
                    stopSource.Dispose();
                    stopSource = null;
                    RestoreMainWindowIfNeeded();
                    EndPreparing();
                    return;
                }

                _state = RecordingState.Starting;
                _isPreparing = false;
                _control = control;
                _activeTarget = target;
                _stopSource = stopSource;
            }

            var recordingTask = RunRecordingAsync(target, outputPath, settings, control, stopSource);
            lock (_sync)
            {
                _recordingTask = recordingTask;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            stopSource?.Dispose();
            _logger.LogError(exception, "建立錄影輸出路徑失敗。");
            RestoreMainWindowIfNeeded();
            EndPreparing();
            _notifications.Show(AppStrings.FormatRecordingFailed(exception.Message), isError: true);
        }
    }

    private async Task RunRecordingAsync(
        RecordingTarget target,
        string outputPath,
        RecordingSettings settings,
        RecordingControl control,
        CancellationTokenSource stopSource)
    {
        try
        {
            var options = new RecordingOptions(
                outputPath,
                checked((uint)settings.FrameRate),
                checked((uint)settings.Bitrate),
                settings.IncludeCursor,
                settings.IncludeSystemAudio,
                settings.SystemAudioGain);
            var wait = (CancellationToken cancellationToken) => WaitBeforeRecordingAsync(
                target,
                settings.DelaySeconds,
                cancellationToken);
            var result = target.Kind == RecordingTargetKind.Window
                ? await _recordingService.RecordWindowAsync(
                    target.Handle,
                    options,
                    control,
                    stopSource.Token,
                    wait,
                    MarkRecordingStarted).ConfigureAwait(false)
                : await _recordingService.RecordMonitorAsync(
                    target.Handle,
                    options,
                    control,
                    stopSource.Token,
                    wait,
                    MarkRecordingStarted).ConfigureAwait(false);
            _logger.LogInformation(
                "螢幕錄影完成：{Path}，停止原因：{Reason}，時長：{Duration}",
                result.OutputPath,
                result.StopReason,
                control.Elapsed);
            lock (_sync)
            {
                _lastOutputPath = result.OutputPath;
            }

            _notifications.Show(AppStrings.FormatRecordingSaved(result.OutputPath, result.StopReason));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "螢幕錄影失敗。");
            _notifications.Show(AppStrings.FormatRecordingFailed(exception.Message), isError: true);
        }
        finally
        {
            _overlay.Hide();
            lock (_sync)
            {
                if (ReferenceEquals(_stopSource, stopSource))
                {
                    _stopSource = null;
                    _recordingTask = null;
                    _control = null;
                    _activeTarget = null;
                    _state = RecordingState.Idle;
                    _remainingDelaySeconds = null;
                }
            }

            stopSource.Dispose();
            RestoreMainWindowIfNeeded();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MarkRecordingStarted()
    {
        RecordingTarget? target;
        lock (_sync)
        {
            if (_state == RecordingState.Stopping)
            {
                return;
            }

            _remainingDelaySeconds = null;
            _state = RecordingState.Recording;
            target = _activeTarget;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
        if (target is not null)
        {
            _overlay.Show(
                target,
                () => Elapsed,
                PauseAsync,
                ResumeAsync,
                StopAsync);
        }

        if (target?.IsWindow == true)
        {
            _notifications.Show(AppStrings.RecordingStarted, playSound: false);
        }
    }

    private async Task WaitBeforeRecordingAsync(
        RecordingTarget target,
        int delaySeconds,
        CancellationToken cancellationToken)
    {
        if (delaySeconds > 0)
        {
            lock (_sync)
            {
                if (_state == RecordingState.Stopping)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _state = RecordingState.Countdown;
                _remainingDelaySeconds = delaySeconds;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
            var completed = await _countdown.ShowAsync(
                target,
                delaySeconds,
                UpdateRemainingDelay,
                cancellationToken).ConfigureAwait(false);
            if (!completed)
            {
                throw new OperationCanceledException("錄影倒數已取消。");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryRefreshTarget(target, out var refreshed))
        {
            throw new InvalidOperationException(AppStrings.RecordingTargetUnavailable);
        }

        lock (_sync)
        {
            _remainingDelaySeconds = null;
            _activeTarget = refreshed;
            if (_state != RecordingState.Stopping)
            {
                _state = RecordingState.Starting;
            }
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryRefreshTarget(RecordingTarget target, out RecordingTarget refreshed)
    {
        if (target.IsWindow)
        {
            var windowTarget = new WindowCaptureTarget(target.Handle, target.ProcessId);
            if (!WindowHelper.TryGetVisibleWindowBounds(windowTarget, out var bounds))
            {
                refreshed = target;
                return false;
            }

            var monitor = _monitors.GetMonitorForRect(bounds) ?? target.Monitor;
            refreshed = target with { Bounds = bounds, Monitor = monitor };
            return true;
        }

        var monitorInfo = _monitors.GetMonitors().FirstOrDefault(item => item.Handle == target.Handle);
        if (monitorInfo is null)
        {
            refreshed = target;
            return false;
        }

        refreshed = target with { Bounds = monitorInfo.Bounds, Monitor = monitorInfo };
        return true;
    }

    private void UpdateRemainingDelay(int seconds)
    {
        lock (_sync)
        {
            _remainingDelaySeconds = seconds;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreMainWindowIfNeeded()
    {
        if (!_restoreMainWindow)
        {
            return;
        }

        _restoreMainWindow = false;
        var window = MainWindow;
        if (window is null)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            RestoreWindow(window);
        }
        else
        {
            _ = dispatcher.BeginInvoke(() => RestoreWindow(window));
        }
    }

    private static void RestoreWindow(Window window)
    {
        if (!window.IsVisible)
        {
            window.Show();
        }

        window.Activate();
    }

    private static string CreateOutputPath(string directory)
    {
        var createdAt = DateTimeOffset.Now;
        var path = Path.Combine(directory, PathUtil.BuildRecordingFileName(createdAt));
        for (var suffix = 2; File.Exists(path) || File.Exists(path + ".recording.tmp"); suffix++)
        {
            path = Path.Combine(
                directory,
                $"Clippings_{createdAt:yyyyMMdd_HHmmss}_{suffix}.mp4");
        }

        return path;
    }

    private static void EnsureRecordingDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        var root = Path.GetPathRoot(directory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        if (drive.IsReady && drive.AvailableFreeSpace < MinimumFreeSpaceBytes)
        {
            throw new IOException("可用磁碟空間少於 256 MB，無法開始錄影。");
        }
    }

    private string GetConfiguredOutputDirectory()
    {
        var configured = _appState.Settings.Recording.OutputDirectory;
        return string.IsNullOrWhiteSpace(configured)
            ? PathUtil.DefaultRecordingRoot
            : Path.GetFullPath(configured);
    }

    private bool TryOpen(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(exception, "無法開啟錄影路徑：{Path}", path);
            _notifications.Show(AppStrings.FormatOpenRecordingFailed(exception.Message), isError: true);
            return false;
        }
    }

    private void CleanupStaleTemporaryFiles()
    {
        try
        {
            var directories = new[] { PathUtil.DefaultRecordingRoot, GetConfiguredOutputDirectory() }
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(directory, "*.recording.tmp"))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _logger.LogWarning(exception, "清理未完成的錄影暫存檔失敗。");
        }
    }
}
