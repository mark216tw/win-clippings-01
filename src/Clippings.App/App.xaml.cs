using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using Clippings.App.Helpers;
using Clippings.App.Logging;
using Clippings.App.Models;
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
using Microsoft.Extensions.Logging;

namespace Clippings.App;

public partial class App : Application
{
    private const int ExitCodeSuccess = 0;
    private const int ExitCodeForwardFailed = 1;
    private const int ExitCodeInvalidArguments = 2;

    private ServiceProvider? _provider;
    private IServiceProvider? _services;
    private SingleInstanceService? _singleInstance;
    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private TrayNotificationService? _notifications;
    private ScreenAnnotationService? _annotations;
    private RecordingCoordinator? _recording;
    private MenuItem? _startRecordingMenuItem;
    private MenuItem? _startFullScreenRecordingMenuItem;
    private MenuItem? _stopRecordingMenuItem;
    private MenuItem? _openRecordingMenuItem;
    private IReadOnlyList<string> _hotkeyFailures = [];
    private ILogger<App>? _logger;
    private bool _exitRequested;
    private bool _resourcesLoaded;
    private volatile bool _mainWindowVisible;

    public static bool IsDarkTheme { get; private set; }

    public static new App Current => (App)Application.Current;

    public bool IsExiting { get; private set; }

    internal bool IsMainWindowVisible => _mainWindowVisible;

    internal void SetMainWindowVisible(bool visible) => _mainWindowVisible = visible;

    public AppState? State { get; private set; }

    public ThemeService? ThemeManager { get; private set; }

    public CaptureCoordinator? Coordinator { get; private set; }

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

        _provider = BuildServices(_singleInstance);
        _services = _provider;
        _logger = _services.GetRequiredService<ILogger<App>>();
        if (!AppIdentity.TryApply())
        {
            _logger.LogWarning("無法設定 Windows AppUserModelID。");
        }

        _notifications = _services.GetRequiredService<TrayNotificationService>();
        State = _services.GetRequiredService<AppState>();
        State.SettingsChanged += OnSettingsChanged;

        if (!options.HideMainWindow)
        {
            EnsureApplicationResources();
            EnsureMainWindow().Show();
        }

        CreateTrayIcon();

        _hotkeyFailures = ApplyHotkeys();
        foreach (var failure in _hotkeyFailures)
        {
            _logger.LogWarning("快捷鍵無法註冊：{Hotkey}", failure);
        }

        if (_hotkeyFailures.Count > 0)
        {
            _notifications?.Show("部分快捷鍵無法啟用，可能已被其他程式使用。", isError: true);
        }

        _singleInstance.StartListening();
        _singleInstance.ActivationRequested += OnActivationRequested;

        var startWithWindows = State.Settings.StartWithWindows;
        _ = Task.Run(() =>
        {
            StartupRegistrar.SetEnabled(startWithWindows);
            _ = LegacyDataCleanup.RunAsync(PathUtil.DefaultDataRoot);
        });

        ExecuteStartupCommand(options.Command);

        if (options.HideMainWindow && options.Command == StartupCommand.None)
        {
            MemoryTrimmer.ScheduleTrim(
                delayMilliseconds: 1500,
                shouldTrim: () => !IsMainWindowVisible && !IsExiting);
        }
    }

    private void EnsureApplicationResources()
    {
        if (_resourcesLoaded)
        {
            return;
        }

        _resourcesLoaded = true;
        var merged = Application.Current.Resources.MergedDictionaries;
        merged.Add(new ResourceDictionary { Source = new Uri("Styles/Controls.xaml", UriKind.Relative) });
        merged.Add(new ResourceDictionary { Source = new Uri("Styles/Logo.xaml", UriKind.Relative) });
        ThemeManager ??= _services?.GetRequiredService<ThemeService>();
        if (State is not null)
        {
            ApplyTheme(State.Settings.Theme);
        }
    }

    private CaptureCoordinator EnsureCoordinator()
    {
        if (Coordinator is not null)
        {
            return Coordinator;
        }

        if (_services is null)
        {
            throw new InvalidOperationException("應用程式尚未完成初始化。");
        }

        var coordinator = _services.GetRequiredService<CaptureCoordinator>();
        coordinator.MainWindow = _mainWindow;
        coordinator.AnnotationService = _annotations;
        coordinator.CaptureAdded += OnCaptureAdded;
        Coordinator = coordinator;
        return coordinator;
    }

    private RecordingCoordinator EnsureRecording()
    {
        if (_recording is not null)
        {
            return _recording;
        }

        if (_services is null)
        {
            throw new InvalidOperationException("應用程式尚未完成初始化。");
        }

        var recording = _services.GetRequiredService<RecordingCoordinator>();
        recording.MainWindow = _mainWindow;
        recording.StateChanged += OnRecordingStateChanged;
        _recording = recording;
        return recording;
    }

    private ScreenAnnotationService EnsureAnnotations()
    {
        if (_annotations is not null)
        {
            return _annotations;
        }

        if (_services is null)
        {
            throw new InvalidOperationException("應用程式尚未完成初始化。");
        }

        var coordinator = EnsureCoordinator();
        var annotations = _services.GetRequiredService<ScreenAnnotationService>();
        annotations.CaptureCoordinator = coordinator;
        coordinator.AnnotationService = annotations;
        _annotations = annotations;
        return annotations;
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
        if (_services is null || command is StartupCommand.None or StartupCommand.Help or StartupCommand.Show)
        {
            return;
        }

        switch (command)
        {
            case StartupCommand.CaptureRegion:
                _ = EnsureCoordinator().CaptureRegionAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureFullScreen:
                _ = EnsureCoordinator().CaptureFullScreenAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureMonitor:
                _ = EnsureCoordinator().CaptureMonitorAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.CaptureWindow:
                _ = EnsureCoordinator().CaptureWindowAsync(CaptureInvocationSource.CommandLine);
                break;
            case StartupCommand.Annotate:
                EnsureAnnotations().Toggle();
                break;
        }
    }

    private ServiceProvider BuildServices(SingleInstanceService singleInstance)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
#if DEBUG
            logging.AddDebug();
#endif
            logging.AddProvider(new FileLoggerProvider(FileLoggerProvider.DefaultDirectory));
            logging.SetMinimumLevel(LogLevel.Information);
        });

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
        services.AddSingleton<CaptureWorkspaceService>();
        services.AddSingleton<CaptureCoordinator>();
        services.AddSingleton<RecordingCoordinator>();
        services.AddSingleton<ScreenAnnotationService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<CaptureWorkspacePage>();
        services.AddSingleton<SettingsPage>();

        return services.BuildServiceProvider();
    }

    private void CreateTrayIcon()
    {
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick;
        _trayIcon.LeftClickCommand = new RelayCommand(ShowMainWindow);
        _trayIcon.ForceCreate();
        _notifications?.Initialize(_trayIcon);
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(AttachTrayMenu));
    }

    private void AttachTrayMenu()
    {
        if (_trayIcon is null || IsExiting)
        {
            return;
        }

        EnsureApplicationResources();
        _trayIcon.ContextMenu = BuildTrayMenu();
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
        };

        menu.Items.Add(CreateMenuItem($"開啟 {AppStrings.AppName}", ShowMainWindow));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("區域擷取", () => _ = EnsureCoordinator().CaptureRegionAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("全螢幕擷取", () => _ = EnsureCoordinator().CaptureFullScreenAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("目前螢幕擷取", () => _ = EnsureCoordinator().CaptureMonitorAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("目前視窗擷取", () => _ = EnsureCoordinator().CaptureWindowAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateMenuItem("延遲擷取", () => _ = EnsureCoordinator().CaptureDelayedAsync(CaptureInvocationSource.QuickCapture)));
        menu.Items.Add(CreateSeparator());
        _startRecordingMenuItem = CreateMenuItem(AppStrings.StartWindowRecording, () => _ = EnsureRecording().StartWithSelectionAsync());
        _startFullScreenRecordingMenuItem = CreateMenuItem(AppStrings.StartFullScreenRecording, () => _ = EnsureRecording().StartFullScreenAsync());
        _stopRecordingMenuItem = CreateMenuItem(AppStrings.StopRecording, () => _ = _recording?.StopAsync());
        _openRecordingMenuItem = CreateMenuItem(AppStrings.OpenLastRecording, () => _ = _recording?.OpenLastRecording());
        menu.Items.Add(_startFullScreenRecordingMenuItem);
        menu.Items.Add(_startRecordingMenuItem);
        menu.Items.Add(_stopRecordingMenuItem);
        menu.Items.Add(_openRecordingMenuItem);
        menu.Items.Add(CreateMenuItem(AppStrings.OpenRecordingFolder, () => _ = EnsureRecording().OpenRecordingFolder()));
        UpdateRecordingMenuItems();
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("螢幕標註", () => EnsureAnnotations().Toggle()));
        menu.Items.Add(CreateMenuItem("開啟資料夾", OpenDataFolder));
        menu.Items.Add(CreateMenuItem("設定", () => ShowMainWindow(1)));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("結束", RequestExit));

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
        if (_startRecordingMenuItem is null || _startFullScreenRecordingMenuItem is null || _stopRecordingMenuItem is null || _openRecordingMenuItem is null)
        {
            return;
        }

        var state = _recording?.State ?? RecordingState.Idle;
        var idle = state == RecordingState.Idle;
        var canStart = _recording?.CanStartRecording ?? OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362);
        _startRecordingMenuItem.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        _startFullScreenRecordingMenuItem.Visibility = idle ? Visibility.Visible : Visibility.Collapsed;
        _startRecordingMenuItem.IsEnabled = canStart;
        _startFullScreenRecordingMenuItem.IsEnabled = canStart;
        _stopRecordingMenuItem.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;
        _stopRecordingMenuItem.IsEnabled = state is RecordingState.Starting or RecordingState.Countdown or RecordingState.Recording or RecordingState.Paused;
        _openRecordingMenuItem.IsEnabled = _recording?.LastOutputPath is { } path && File.Exists(path);
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

    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo(PathUtil.DefaultDataRoot) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger?.LogWarning(exception, "無法開啟資料夾。");
            _notifications?.Show("無法開啟資料夾。", isError: true);
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
        var brush = new SolidColorBrush(Color.FromRgb(channel, channel, channel));
        brush.Freeze();
        return brush;
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

    private void ShowMainWindow() => ShowMainWindow(0);

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow is not null)
        {
            return _mainWindow;
        }

        if (_services is null)
        {
            throw new InvalidOperationException("應用程式尚未完成初始化。");
        }

        EnsureApplicationResources();
        var coordinator = EnsureCoordinator();
        var recording = EnsureRecording();
        _ = EnsureAnnotations();

        var window = _services.GetRequiredService<MainWindow>();
        _mainWindow = window;
        MainWindow = window;
        coordinator.MainWindow = window;
        recording.MainWindow = window;
        window.UpdateHotkeyStatus(_hotkeyFailures);
        return window;
    }

    private void OnCaptureAdded(object? sender, CaptureAddedEventArgs e)
    {
        if (!e.ShowWindow || IsExiting)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            _ = ShowCapturedTabAsync(e.Tab);
        }
        else
        {
            _ = Dispatcher.BeginInvoke(() => _ = ShowCapturedTabAsync(e.Tab));
        }
    }

    private async Task ShowCapturedTabAsync(WorkspaceTab tab)
    {
        ShowMainWindow(0);
        var window = _mainWindow;
        if (window is not null)
        {
            window.Activate();
            await window.ShowWorkspaceTabAsync(tab);
        }
    }

    private void ShowMainWindow(int pageIndex)
    {
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
        if (_services is null || State is null)
        {
            return [];
        }

        var hotkeys = _services.GetRequiredService<GlobalHotkeyService>();
        hotkeys.UnregisterAll();
        var failures = new List<string>();
        var settings = State.Settings;

        Register(settings.HotkeyRegion, () => _ = EnsureCoordinator().CaptureRegionAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyFullScreen, () => _ = EnsureCoordinator().CaptureFullScreenAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyWindow, () => _ = EnsureCoordinator().CaptureWindowAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyDelayed, () => _ = EnsureCoordinator().CaptureDelayedAsync(CaptureInvocationSource.GlobalHotkey));
        Register(settings.HotkeyAnnotate, () => EnsureAnnotations().Toggle());

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

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (State is null || _services is null)
        {
            return;
        }

        var settings = State.Settings;
        ApplyTheme(settings.Theme);
        StartupRegistrar.SetEnabled(settings.StartWithWindows);
        _mainWindow?.RefreshSettingsSummary();
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

    public void RequestExit()
    {
        if (IsExiting || _exitRequested)
        {
            return;
        }

        if (_recording is { State: RecordingState.Recording or RecordingState.Paused })
        {
            ShowExitBlockedMessage();
            return;
        }

        _exitRequested = true;
        _ = ExitApplicationAsync();
    }

    private void ShowExitBlockedMessage()
    {
        if (_mainWindow is { IsVisible: true } window)
        {
            MessageBox.Show(
                window,
                AppStrings.ExitBlockedByRecording,
                AppStrings.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _notifications?.Show(AppStrings.ExitBlockedByRecording, isError: true);
    }

    private async Task ExitApplicationAsync()
    {
        if (_mainWindow is not null)
        {
            try
            {
                await _mainWindow.FlushWorkspaceAsync();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(exception, "結束程式前暫存擷圖失敗。");
            }
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

        _notifications?.Initialize(null);
        _trayIcon?.Dispose();

        _provider?.Dispose();

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








