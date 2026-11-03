using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Clippings.App.Services;
using Clippings.Platform.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace Clippings.App.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private readonly AppState _state;
    private readonly CaptureCoordinator _coordinator;

    private HistoryPage? _historyPage;
    private ClipboardPage? _clipboardPage;
    private SettingsPage? _settingsPage;
    private bool _loadedOnce;
    private bool _suppressNavigation;
    private int _currentPageIndex;

    public MainWindow(
        IServiceProvider services,
        AppState state,
        CaptureCoordinator coordinator)
    {
        InitializeComponent();

        _services = services;
        _state = state;
        _coordinator = coordinator;

        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        VersionText.Text = string.IsNullOrWhiteSpace(informational)
            ? "v1.8.4"
            : $"v{informational.Split('+')[0]}";
        RefreshSettingsSummary();
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
        NavigateTo(NavList.SelectedIndex < 0 ? 0 : NavList.SelectedIndex);
    }

    public void NavigateTo(int index)
    {
        var safeIndex = Math.Clamp(index, 0, 2);
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
        => target == _currentPageIndex || _currentPageIndex != 2 || _settingsPage?.ConfirmLeave() != false;

    private void RestoreNavigationSelection()
    {
        _suppressNavigation = true;
        NavList.SelectedIndex = _currentPageIndex;
        _suppressNavigation = false;
    }

    private void ShowPage(int index)
    {
        if (_currentPageIndex == 2 && index != 2)
        {
            _settingsPage?.StopHotkeyRecording();
        }

        switch (index)
        {
            case 0:
                _historyPage ??= _services.GetRequiredService<HistoryPage>();
                ContentHost.Content = _historyPage;
                _ = _historyPage.ReloadAsync();
                break;
            case 1:
                _clipboardPage ??= _services.GetRequiredService<ClipboardPage>();
                ContentHost.Content = _clipboardPage;
                _ = _clipboardPage.ReloadAsync();
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

    public void NotifyCaptureStored()
    {
        if (_historyPage is not null && ReferenceEquals(ContentHost.Content, _historyPage))
        {
            _ = _historyPage.ReloadAsync();
        }
    }

    public void NotifyCaptureDeleted()
    {
        if (_historyPage is not null && ReferenceEquals(ContentHost.Content, _historyPage))
        {
            _ = _historyPage.ReloadAsync();
        }
    }

    public void NotifyClipboardStored()
    {
        if (_clipboardPage is not null && ReferenceEquals(ContentHost.Content, _clipboardPage))
        {
            _ = _clipboardPage.ReloadAsync();
        }
    }

    private void OnCaptureClick(object sender, RoutedEventArgs e)
    {
        _ = ((sender as FrameworkElement)?.Tag as string) switch
        {
            "FullScreen" => _coordinator.CaptureFullScreenAsync(),
            "Monitor" => _coordinator.CaptureMonitorAsync(),
            "Window" => _coordinator.CaptureWindowAsync(),
            "Delayed" => _coordinator.CaptureDelayedAsync(),
            _ => _coordinator.CaptureRegionAsync(),
        };
    }

    public void RefreshSettingsSummary()
    {
        var settings = _state.Settings;
        HotkeyHint.Text = $"區域 {settings.HotkeyRegion}\n全螢幕 {settings.HotkeyFullScreen}\n視窗 {settings.HotkeyWindow}\n延遲 {settings.HotkeyDelayed}";
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
        if (e.Key != Key.F || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (_currentPageIndex == 0)
        {
            _historyPage?.FocusSearch();
        }

        e.Handled = _currentPageIndex == 0;
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
            Hide();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _settingsPage?.StopHotkeyRecording();
        _historyPage = null;
        _clipboardPage = null;
        _settingsPage = null;
    }
}




