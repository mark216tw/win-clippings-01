using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Clippings.App.Helpers;
using Clippings.App.Logging;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.App.Views;
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
    private ILogger<App>? _logger;

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
        _annotations = _services.GetRequiredService<ScreenAnnotationService>();
        _annotations.CaptureCoordinator = Coordinator;
        Coordinator.AnnotationService = _annotations;

        ApplyTheme(State.Settings.Theme);

        CreateTrayIcon();
        StartClipboardWatcher();
        StartupRegistrar.SetEnabled(State.Settings.StartWithWindows);

        _mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = _mainWindow;
        Coordinator.MainWindow = _mainWindow;
        Coordinator.CaptureStored += (_, _) => _mainWindow.NotifyCaptureStored();
        State.SettingsChanged += OnSettingsChanged;

        var hotkeyFailures = ApplyHotkeys();
        foreach (var failure in hotkeyFailures)
        {
            _logger.LogWarning("快捷鍵無法註冊：{Hotkey}", failure);
        }

        if (!options.HideMainWindow)
        {
            _mainWindow.Show();
        }

        if (hotkeyFailures.Count > 0)
        {
            _notifications?.Show("部分快捷鍵無法啟用，可能已被其他程式使用。", isError: true);
        }

        _singleInstance.StartListening();
        _singleInstance.ActivationRequested += OnActivationRequested;

        ExecuteStartupCommand(options.Command);
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
                services.AddSingleton<ForegroundWindowTracker>();
                services.AddSingleton<IClippingsStore>(_ =>
                {
                    var store = new ClippingsStore();
                    store.Initialize();
                    return store;
                });
                services.AddSingleton<CaptureCoordinator>();
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
        menu.Opened += (_, _) => ApplyWindowsThemeToTrayMenu(menu);

        menu.Items.Add(CreateMenuItem("開啟 Clippings", ShowMainWindow));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("區域擷取", () => _ = Coordinator!.CaptureRegionAsync()));
        menu.Items.Add(CreateMenuItem("全螢幕擷取", () => _ = Coordinator!.CaptureFullScreenAsync()));
        menu.Items.Add(CreateMenuItem("目前螢幕擷取", () => _ = Coordinator!.CaptureMonitorAsync()));
        menu.Items.Add(CreateMenuItem("目前視窗擷取", () => _ = Coordinator!.CaptureWindowAsync()));
        menu.Items.Add(CreateMenuItem("延遲擷取", () => _ = Coordinator!.CaptureDelayedAsync()));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("螢幕標註", () => _annotations?.Toggle()));
        menu.Items.Add(CreateMenuItem("剪貼簿歷史", () => ShowMainWindow(1)));
        menu.Items.Add(CreateMenuItem("設定", () => ShowMainWindow(2)));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateMenuItem("結束", ExitApplication));

        return menu;
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

    private void ShowMainWindow() => ShowMainWindow(0);

    private void ShowMainWindow(int pageIndex)
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.NavigateTo(pageIndex);
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
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
        IsExiting = true;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _mainWindow = null;
        _annotations?.Stop();

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








