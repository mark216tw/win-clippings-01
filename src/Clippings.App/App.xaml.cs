using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Clippings.App.Helpers;
using Clippings.App.Logging;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.App.Views;
using Clippings.Core.Helpers;
using Clippings.Core.Input;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Core.Startup;
using Clippings.Platform.Capture;
using Clippings.Platform.Display;
using Clippings.Platform.Interop;
using Clippings.Platform.SingleInstance;
using Clippings.Platform.Storage;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Clippings.App;

public partial class App : Application
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeForwardFailed = 1;
    private const int ExitCodeInvalidArguments = 2;

    private IHost? _host;
    private IServiceProvider? _services;
    private SingleInstanceService? _singleInstance;
    private ClipboardWatcher? _clipboardWatcher;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private TrayNotificationService? _notifications;
    private ScreenAnnotationService? _annotations;
    private RecordingCoordinator? _recording;
    private MenuItem? _startRecordingMenuItem;
    private MenuItem? _startFullScreenRecordingMenuItem;
    private MenuItem? _stopRecordingMenuItem;
    private MenuItem? _openRecordingMenuItem;
    private MenuItem? _clipboardHistoryMenuItem;
    private ILogger<App>? _logger;
    private IReadOnlyList<string> _hotkeyFailures = [];

    public static bool IsDarkTheme { get; private set; }

    public static new App Current => (App)Application.Current;

    public bool IsExiting { get; private set; }

    public AppState? State { get; private set; }

    public ThemeService? ThemeManager { get; private set; }

    public CaptureCoordinator? Coordinator { get; private set; }

    public IClippingsStore? ClippingsStore { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!CommandLineOptions.TryParse(e.Args, out var options, out var invalidArgument))
        {
            ReportCommandLine(
                AppStrings.FormatInvalidCommandLineArgument(invalidArgument ?? string.Empty)
                    + Environment.NewLine + Environment.NewLine + AppStrings.CommandLineUsage,
                MessageBoxImage.Warning);
            Shutdown(ExitCodeInvalidArguments);
            return;
        }

        if (options.Command == StartupCommand.Help)
        {
            ReportCommandLine(AppStrings.CommandLineUsage, MessageBoxImage.Information);
            Shutdown(ExitCodeSuccess);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        TextBoxContextMenuHelper.Register();

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            var forwarded = _singleInstance.NotifyExistingInstance(e.Args);
            _singleInstance.Dispose();
            if (!forwarded)
            {
                ReportCommandLine(AppStrings.CommandLineForwardFailed, MessageBoxImage.Warning);
            }

            Environment.Exit(forwarded ? ExitCodeSuccess : ExitCodeForwardFailed);
            return;
        }

        _host = BuildHost(_singleInstance);
        _host.Start();
        _services = _host.Services;
        _logger = _services.GetRequiredService<ILogger<App>>();
        _notifications = _services.GetRequiredService<TrayNotificationService>();

        State = _services.GetRequiredService<AppState>();
        ThemeManager = _services.GetRequiredService<ThemeService>();
        ClippingsStore = _services.GetRequiredService<IClippingsStore>();
        Coordinator = _services.GetRequiredService<CaptureCoordinator>();
        _recording = _services.GetRequiredService<RecordingCoordinator>();
        _recording.StateChanged += OnRecordingStateChanged;
        _annotations = _services.GetRequiredService<ScreenAnnotationService>();
        _annotations.CaptureCoordinator = Coordinator;
        Coordinator.AnnotationService = _annotations;
        Coordinator.CaptureStored += OnCaptureStored;
        State.SettingsChanged += OnSettingsChanged;

        _ = InitializeStoreAsync();

        var hotkeyFailures = ApplyHotkeys();
        foreach (var failure in hotkeyFailures)
        {
            _logger.LogWarning("快捷鍵無法註冊：{Hotkey}", failure);
        }

        ApplyTheme(State.Settings.Theme);

        if (!options.HideMainWindow)
        {
            EnsureMainWindow().Show();
        }

        _singleInstance.StartListening();
        _singleInstance.ActivationRequested += OnActivationRequested;

        ExecuteStartupCommand(options.Command);

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (IsExiting)
            {
                return;
            }

            CreateTrayIcon();
            StartClipboardWatcher();
            StartupRegistrar.SetEnabled(State?.Settings.StartWithWindows ?? false);

            if (hotkeyFailures.Count > 0)
            {
                _notifications?.Show("部分快捷鍵無法啟用，可能已被其他程式使用。", isError: true);
            }
        }));
    }

    private async Task InitializeStoreAsync()
    {
        try
        {
            if (ClippingsStore is not null)
            {
                await ClippingsStore.InitializeAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "初始化 Clippings 資料庫失敗。");
        }
    }

    private static void ReportCommandLine(string message, MessageBoxImage icon)
    {
        if (ConsoleBridge.TryAttachToParent())
        {
            ConsoleBridge.WriteLine(message);
            return;
        }

        MessageBox.Show(message, AppStrings.AppName, MessageBoxButton.OK, icon);
    }

    private void OnActivationRequested(object? sender, ActivationRequestedEventArgs e)
    {
        if (Dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() => HandleActivation(e.Arguments)));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void HandleActivation(IReadOnlyList<string> arguments)
    {
        if (!CommandLineOptions.TryParse(arguments, out var options, out var invalidArgument))
        {
            _logger?.LogWarning("收到無效的啟動參數：{Argument}", invalidArgument);
            ShowMainWindow();
            return;
        }

        switch (options.Command)
        {
            case StartupCommand.Help:
                break;
            case StartupCommand.None:
                if (!options.AutoStart)
                {
                    ShowMainWindow();
                }

                break;
            case StartupCommand.Show:
                ShowMainWindow();
                break;
            default:
                ExecuteStartupCommand(options.Command);
                break;
        }
    }

    private void ExecuteStartupCommand(StartupCommand command)
    {
        if (Coordinator is null)
        {
            return;
        }

        switch (command)
        {
            case StartupCommand.CaptureRegion:
                _ = Coordinator.CaptureRegionAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureFullScreen:
                _ = Coordinator.CaptureFullScreenAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureMonitor:
                _ = Coordinator.CaptureMonitorAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureWindow:
                _ = Coordinator.CaptureWindowAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.Annotate:
                _annotations?.Toggle();
                break;
        }
    }

    private IHost BuildHost(SingleInstanceService singleInstance)
    {
        return new HostBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultDirectory));
                logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(singleInstance);
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton<AppState>();
                services.AddSingleton<TrayNotificationService>();
                services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<TrayNotificationService>());
                services.AddSingleton<ThemeService>();
                services.AddSingleton<MonitorService>();
                services.AddSingleton<ScreenCaptureService>();
                services.AddSingleton<WindowsGraphicsCaptureService>();
                services.AddSingleton<WindowsGraphicsRecordingService>();
                services.AddSingleton<ForegroundWindowTracker>();
                services.AddSingleton<WindowEnumerationService>();
                services.AddSingleton<RecordingTargetPicker>();
                services.AddSingleton<RecordingCountdownService>();
                services.AddSingleton<RecordingOverlayService>();
                services.AddSingleton<IClippingsStore>(_ => new ClippingsStore());
                services.AddSingleton<CaptureCoordinator>();
                services.AddSingleton<RecordingCoordinator>();
                services.AddSingleton<ScreenAnnotationService>();
                services.AddSingleton<GlobalHotkeyService>();
                services.AddSingleton<ClipboardWatcher>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<HistoryPage>();
                services.AddSingleton<ClipboardPage>();
                services.AddSingleton<SettingsPage>();
            })
            .Build();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.ContextMenu = BuildTrayMenu();
        _trayIcon.MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick;
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.ForceCreate();
        _notifications?.Initialize(_trayIcon);
    }

    private ContextMenu BuildTrayMenu()
    {
        var menu = new ContextMenu();
        ApplyWindowsThemeToTrayMenu(menu);
        ApplyStyle(menu, "AppContextMenuStyle");
        menu.Opened += (_, _) =>
        {
            ApplyWindowsThemeToTrayMenu(menu);
            UpdateRecordingMenuItems();
            UpdateClipboardHistoryMenuItem();
        };

        menu.Items.Add(CreateMenuItem("開啟 Clippings", ShowMainWindow));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("區域擷取", () => _ = Coordinator!.CaptureRegionAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("全螢幕擷取", () => _ = Coordinator!.CaptureFullScreenAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("目前螢幕擷取", () => _ = Coordinator!.CaptureMonitorAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("目前視窗擷取", () => _ = Coordinator!.CaptureWindowAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("延遲擷取", () => _ = Coordinator!.CaptureDelayedAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateSeparator());
        _startRecordingMenuItem = CreateMenuItem(AppStrings.StartWindowRecording, () => _ = _recording!.StartWithSelectionAsync());
        _startFullScreenRecordingMenuItem = CreateMenuItem(AppStrings.StartFullScreenRecording, () => _ = _recording!.StartFullScreenAsync());
        _stopRecordingMenuItem = CreateMenuItem(AppStrings.StopRecording, () => _ = _recording!.StopAsync());
        _openRecordingMenuItem = CreateMenuItem(AppStrings.OpenLastRecording, () => _ = _recording!.OpenLastRecording());
        menu.Items.Add(_startFullScreenRecordingMenuItem);
        menu.Items.Add(_startRecordingMenuItem);
        menu.Items.Add(_stopRecordingMenuItem);
        menu.Items.Add(_openRecordingMenuItem);
        menu.Items.Add(CreateMenuItem(AppStrings.OpenRecordingFolder, () => _ = _recording!.OpenRecordingFolder()));
        UpdateRecordingMenuItems();
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("螢幕標註", () => _annotations?.Toggle()));
        menu.Items.Add(CreateMenuItem("剪貼簿歷史", () => ShowMainWindow(1)));
        _clipboardHistoryMenuItem = CreateMenuItem("啟用剪貼簿歷史", ToggleClipboardHistory);
        UpdateClipboardHistoryMenuItem();
        menu.Items.Add(_clipboardHistoryMenuItem);
        menu.Items.Add(CreateMenuItem("開啟 Clippings 資料夾", OpenDataFolder));
        menu.Items.Add(CreateMenuItem("設定", () => ShowMainWindow(2)));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("結束", ExitApplication));

        return menu;
    }

    private void OnRecordingStateChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateRecordingMenuItems();
        }
        else
        {
            _ = Dispatcher.BeginInvoke(UpdateRecordingMenuItems);
        }
    }

    private void UpdateRecordingMenuItems()
    {
        if (_recording is null || _startRecordingMenuItem is null || _startFullScreenRecordingMenuItem is null || _stopRecordingMenuItem is null || _openRecordingMenuItem is null)
        {
            return;
        }

        var state = _recording.State;
        var idle = state == RecordingState.Idle;
        _startRecordingMenuItem.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        _startFullScreenRecordingMenuItem.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        _startRecordingMenuItem.IsEnabled = _recording.CanStartRecording;
        _startFullScreenRecordingMenuItem.IsEnabled = _recording.CanStartRecording;
        _stopRecordingMenuItem.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;
        _stopRecordingMenuItem.IsEnabled = state is RecordingState.Starting or RecordingState.Countdown or RecordingState.Recording or RecordingState.Paused;
        _openRecordingMenuItem.IsEnabled = _recording.LastOutputPath is { } path && File.Exists(path);
        if (_trayIcon is not null)
        {
            _trayIcon.ToolTipText = state switch
            {
                RecordingState.Recording => AppStrings.RecordingTrayToolTip,
                RecordingState.Paused => AppStrings.RecordingTrayToolTipPaused,
                _ => AppStrings.AppName,
            };
        }
    }

    private void UpdateClipboardHistoryMenuItem()
    {
        if (_clipboardHistoryMenuItem is not null && State is not null)
        {
            _clipboardHistoryMenuItem.Header = State.Settings.ClipboardHistoryEnabled
                ? "暫停剪貼簿歷史"
                : "啟用剪貼簿歷史";
        }
    }

    private void ToggleClipboardHistory()
    {
        if (State is null)
        {
            return;
        }

        var settings = State.Settings.Clone();
        settings.ClipboardHistoryEnabled = !settings.ClipboardHistoryEnabled;
        State.Save(settings);
    }

    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(PathUtil.DefaultDataRoot) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger?.LogWarning(exception, "無法開啟 Clippings 資料夾。");
            _notifications?.Show("無法開啟 Clippings 資料夾。", isError: true);
        }
    }

    private static void ApplyWindowsThemeToTrayMenu(ContextMenu menu)
    {
        var dark = ThemeService.IsSystemDark();
        menu.Resources["SurfaceBrush"] = CreateBrush(dark ? 0x2C : 0xF9);
        menu.Resources["BorderBrush"] = CreateBrush(dark ? 0x45 : 0xDA);
        menu.Resources["TextPrimaryBrush"] = CreateBrush(dark ? 0xFF : 0x1A);
        menu.Resources["TextSecondaryBrush"] = CreateBrush(dark ? 0xC7 : 0x61);
        menu.Resources["HoverBrush"] = CreateBrush(dark ? 0x3D : 0xE5);
    }

    private static SolidColorBrush CreateBrush(int value)
    {
        var channel = (byte)value;
        return new SolidColorBrush(Color.FromRgb(channel, channel, channel));
    }

    private static MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        ApplyStyle(item, "AppMenuItemStyle");
        item.Click += (_, _) => action();
        return item;
    }

    private static Separator CreateSeparator()
    {
        var separator = new Separator();
        ApplyStyle(separator, "AppSeparatorStyle");
        return separator;
    }

    private static void ApplyStyle(FrameworkElement element, string key)
    {
        if (Application.Current?.TryFindResource(key) is Style style)
        {
            element.Style = style;
        }
    }

    private void ShowMainWindow()
        => ShowMainWindow(0);

    internal void ShowRecentCaptures()
    {
        if (IsExiting)
        {
            return;
        }

        ShowMainWindow(0);
    }

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow is not null)
        {
            return _mainWindow;
        }

        if (_services is null)
        {
            throw new InvalidOperationException("服務尚未初始化，無法建立主視窗。");
        }

        var window = _services.GetRequiredService<MainWindow>();
        _mainWindow = window;
        MainWindow = window;
        Coordinator!.MainWindow = window;
        _recording!.MainWindow = window;
        window.UpdateHotkeyStatus(_hotkeyFailures);
        return window;
    }

    private void OnCaptureStored(object? sender, EventArgs e)
        => _mainWindow?.NotifyCaptureStored();

    private void ShowMainWindow(int pageIndex)
    {
        if (IsExiting || _services is null)
        {
            return;
        }

        var window = EnsureMainWindow();
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.NavigateTo(pageIndex);
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    public IReadOnlyList<string> ApplyHotkeys()
    {
        if (_services is null || State is null || Coordinator is null)
        {
            return [];
        }

        var hotkeys = _services.GetRequiredService<GlobalHotkeyService>();
        hotkeys.UnregisterAll();
        var failures = new List<string>();
        var settings = State.Settings;

        Register(settings.HotkeyRegion, () => _ = Coordinator.CaptureRegionAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyFullScreen, () => _ = Coordinator.CaptureFullScreenAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyWindow, () => _ = Coordinator.CaptureWindowAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyDelayed, () => _ = Coordinator.CaptureDelayedAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyAnnotate, () => _annotations?.Toggle());

        _hotkeyFailures = failures;
        _mainWindow?.UpdateHotkeyStatus(failures);
        return failures;

        void Register(string text, Action action)
        {
            if (!HotkeyGesture.TryParse(text, out var gesture) || gesture is null)
            {
                failures.Add(text);
                return;
            }

            if (!hotkeys.TryRegister(gesture, action))
            {
                failures.Add(gesture.DisplayName);
            }
        }
    }

    private void StartClipboardWatcher()
    {
        if (_services is null)
        {
            return;
        }

        _clipboardWatcher = _services.GetRequiredService<ClipboardWatcher>();
        _clipboardWatcher.Captured += OnClipboardCaptured;
        _clipboardWatcher.Start();
    }

    private async void OnClipboardCaptured(object? sender, ClipboardSnapshotEventArgs e)
    {
        if (_services is null || State is null)
        {
            return;
        }

        var snapshot = e.Snapshot;
        try
        {
            var store = _services.GetRequiredService<IClippingsStore>();
            await store.AddClipboardItemAsync(
                new ClipboardItem
                {
                    CreatedAt = DateTimeOffset.Now,
                    Kind = ClipboardItemKind.Image,
                    Hash = snapshot.Hash,
                },
                snapshot.ImageBytes,
                snapshot.ThumbnailBytes);

            var count = await store.CountClipboardItemsAsync();
            if (count > State.Settings.MaxClipboardItems + 20)
            {
                await store.TrimClipboardItemsAsync(State.Settings.MaxClipboardItems, State.Settings.RetentionDays);
            }

            _mainWindow?.NotifyClipboardStored();
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "寫入剪貼簿歷史失敗。");
            _notifications?.Show(AppStrings.ClipboardHistorySaveFailed, isError: true);
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (State is null || _services is null)
        {
            return;
        }

        var settings = State.Settings;
        ApplyTheme(settings.Theme);
        _clipboardWatcher?.SetEnabled(settings.ClipboardHistoryEnabled);
        StartupRegistrar.SetEnabled(settings.StartWithWindows);
        _mainWindow?.RefreshSettingsSummary();
        UpdateClipboardHistoryMenuItem();
    }

    internal void ApplyTheme(AppTheme theme)
    {
        if (ThemeManager is null)
        {
            return;
        }

        ThemeManager.Apply(theme);
        IsDarkTheme = ThemeManager.IsDark;
    }

    private void ExitApplication()
    {
        foreach (var editor in Windows.OfType<EditorWindow>())
        {
            editor.PersistWindowPlacement();
        }

        IsExiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _mainWindow = null;
        _annotations?.Stop();

        if (_recording is not null)
        {
            _recording.StateChanged -= OnRecordingStateChanged;
            try
            {
                _recording.StopAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException)
            {
                _logger?.LogWarning(exception, "結束程式時等待錄影完成逾時。");
            }
        }

        if (_clipboardWatcher is not null)
        {
            _clipboardWatcher.Captured -= OnClipboardCaptured;
            _clipboardWatcher.Dispose();
        }

        _notifications?.Initialize(null);
        _trayIcon?.Dispose();

        if (_host is not null)
        {
            try
            {
                _host.StopAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            catch (Exception exception) when (exception is OperationCanceledException or TimeoutException)
            {
            }

            _host.Dispose();
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "未處理的 UI 例外。");
        MessageBox.Show("Clippings 發生未預期的錯誤：" + e.Exception.Message, AppStrings.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger?.LogCritical(exception, "未處理的背景例外。");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "未觀察的工作例外。");
        e.SetObserved();
    }
}








