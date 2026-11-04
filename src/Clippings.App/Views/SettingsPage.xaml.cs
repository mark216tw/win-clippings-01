using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Helpers;
using Clippings.Core.Input;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Microsoft.Win32;

namespace Clippings.App.Views;

public partial class SettingsPage : UserControl
{
    private static readonly ThemeOption[] ThemeOptions =
    [
        new(AppTheme.System, AppStrings.ThemeSystem),
        new(AppTheme.Light, AppStrings.ThemeLight),
        new(AppTheme.WarmPaper, AppStrings.ThemeWarmPaper),
        new(AppTheme.SoftSage, AppStrings.ThemeSoftSage),
        new(AppTheme.MistBlueMorning, AppStrings.ThemeMistBlueMorning),
        new(AppTheme.Dark, AppStrings.ThemeDark),
        new(AppTheme.DuskBlue, AppStrings.ThemeDuskBlue),
        new(AppTheme.DuskPurple, AppStrings.ThemeDuskPurple),
        new(AppTheme.InkGreenNight, AppStrings.ThemeInkGreenNight),
    ];

    private static readonly BitrateOption[] RecordingBitrateOptions =
    [
        new(4_000_000, "4 Mbps"),
        new(8_000_000, "8 Mbps"),
        new(12_000_000, "12 Mbps"),
        new(20_000_000, "20 Mbps"),
    ];

    private readonly AppState _state;
    private readonly IClippingsStore _store;
    private readonly GlobalHotkeyService _globalHotkeys;
    private SettingsSnapshot? _baseline;
    private IDisposable? _hotkeyDispatchSuppression;
    private bool _isLoading;
    private bool _isDirty;

    public SettingsPage(AppState state, IClippingsStore store, GlobalHotkeyService globalHotkeys)
    {
        InitializeComponent();

        _state = state;
        _store = store;
        _globalHotkeys = globalHotkeys;

        DelayCombo.ItemsSource = new[] { 3, 5, 10 };
        MaxClipboardCombo.ItemsSource = new[] { 50, 100, 200, 500, 1000 };
        MaxCaptureCombo.ItemsSource = new[] { 50, 100, 200, 500, 1000 };
        RetentionCombo.ItemsSource = new[] { 7, 30, 90, 365 };
        ThemeCombo.ItemsSource = ThemeOptions;
        ThemeCombo.DisplayMemberPath = nameof(ThemeOption.DisplayName);
        ThemeCombo.SelectedValuePath = nameof(ThemeOption.Value);
        FormatCombo.ItemsSource = new[] { "PNG", "JPEG" };
        AnnotateBackdropCombo.ItemsSource = new[] { "跟隨畫面", "白板", "黑板" };
        RecordingFrameRateCombo.ItemsSource = new[] { 30, 60 };
        RecordingDelayCombo.ItemsSource = new[] { 0, 5, 10, 20, 30 };
        RecordingBitrateCombo.ItemsSource = RecordingBitrateOptions;

        LibraryPathText.Text = store.DataRoot;

        LoadSettings();
        ThemeCombo.SelectionChanged += OnThemeSelectionChanged;
        AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => MarkDirty()));
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((_, _) => MarkDirty()));
        AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler((_, _) => MarkDirty()));
        AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler((_, _) => MarkDirty()));
        AddHandler(RangeBase.ValueChangedEvent, new RoutedPropertyChangedEventHandler<double>((_, _) => MarkDirty()));
        Unloaded += (_, _) => StopHotkeyRecording();
    }

    public bool IsDirty => _isDirty;

    public void LoadSettings()
    {
        _isLoading = true;
        try
        {
            var settings = _state.Settings;

            RegionHotkeyBox.Text = settings.HotkeyRegion;
            FullScreenHotkeyBox.Text = settings.HotkeyFullScreen;
            WindowHotkeyBox.Text = settings.HotkeyWindow;
            DelayedHotkeyBox.Text = settings.HotkeyDelayed;
            AnnotateHotkeyBox.Text = settings.HotkeyAnnotate;

            DelayCombo.SelectedItem = settings.DelaySeconds is 3 or 5 or 10 ? settings.DelaySeconds : 5;
            AutoCopyCheck.IsChecked = settings.AutoCopy;
            CaptureSoundCheck.IsChecked = settings.PlayCaptureSound;
            MagnifierCheck.IsChecked = settings.ShowMagnifier;
            AnnotateClickThroughCheck.IsChecked = settings.Annotation.ClickThrough;
            AnnotateBackdropCombo.SelectedIndex = (int)settings.Annotation.Backdrop;
            RecordingDirBox.Text = settings.Recording.OutputDirectory;
            RecordingFrameRateCombo.SelectedItem = settings.Recording.FrameRate is 30 or 60
                ? settings.Recording.FrameRate
                : 30;
            RecordingBitrateCombo.SelectedValue = settings.Recording.Bitrate is 4_000_000 or 8_000_000 or 12_000_000 or 20_000_000
                ? settings.Recording.Bitrate
                : 8_000_000;
            RecordingCursorCheck.IsChecked = settings.Recording.IncludeCursor;
            RecordingSystemAudioCheck.IsChecked = settings.Recording.IncludeSystemAudio;
            RecordingDelayCombo.SelectedItem = settings.Recording.DelaySeconds is 0 or 5 or 10 or 20 or 30
                ? settings.Recording.DelaySeconds
                : 0;

            SaveDirBox.Text = settings.SaveDirectory;
            SaveAutomaticallyCheck.IsChecked = settings.SaveAutomatically;
            FormatCombo.SelectedIndex = settings.DefaultFormat == ImageFormat.Jpeg ? 1 : 0;
            JpegQualitySlider.Value = Math.Clamp(settings.JpegQuality, 1, 100);

            ClipboardHistoryCheck.IsChecked = settings.ClipboardHistoryEnabled;
            MaxClipboardCombo.SelectedItem = settings.MaxClipboardItems is 50 or 100 or 200 or 500 or 1000 ? settings.MaxClipboardItems : 200;

            MaxCaptureCombo.SelectedItem = settings.MaxCaptureItems is 50 or 100 or 200 or 500 or 1000 ? settings.MaxCaptureItems : 200;
            RetentionCombo.SelectedItem = settings.RetentionDays is 7 or 30 or 90 or 365 ? settings.RetentionDays : 30;

            ThemeCombo.SelectedValue = settings.Theme;
            if (ThemeCombo.SelectedIndex < 0)
            {
                ThemeCombo.SelectedValue = AppTheme.System;
            }

            App.Current.ApplyTheme(GetSelectedTheme());
            StartupCheck.IsChecked = settings.StartWithWindows;

            MessageText.Text = string.Empty;
            RegionHint.Text = string.Empty;
            FullScreenHint.Text = string.Empty;
            WindowHint.Text = string.Empty;
            DelayedHint.Text = string.Empty;
            AnnotateHint.Text = string.Empty;
            _isDirty = false;
            _baseline = CaptureSnapshot();
            UpdateDirtyState();
        }
        finally
        {
            _isLoading = false;
        }
    }

    public bool ConfirmLeave()
    {
        if (!HasChanges())
        {
            _isDirty = false;
            UpdateDirtyState();
            return true;
        }

        var result = MessageBox.Show(
            Window.GetWindow(this),
            "設定尚未儲存。要先儲存變更嗎？",
            AppStrings.SettingsTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Cancel)
        {
            return false;
        }

        if (result == MessageBoxResult.No)
        {
            LoadSettings();
            return true;
        }

        return TrySaveSettings();
    }

    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.None)
        {
            e.Handled = true;
            return;
        }

        if (key == Key.Escape)
        {
            e.Handled = true;
            return;
        }

        var modifiers = ToHotkeyModifiers(Keyboard.Modifiers);
        if (modifiers == HotkeyModifiers.None)
        {
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return;
        }

        var gesture = new HotkeyGesture(modifiers, (uint)virtualKey);
        box.Text = gesture.DisplayName;
        box.CaretIndex = box.Text.Length;
        e.Handled = true;
    }

    private void OnHotkeyGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => _hotkeyDispatchSuppression ??= _globalHotkeys.SuppressDispatch();

    private void OnHotkeyLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsHotkeyBox(e.NewFocus))
        {
            return;
        }

        StopHotkeyRecording();
    }

    internal void StopHotkeyRecording()
    {
        _hotkeyDispatchSuppression?.Dispose();
        _hotkeyDispatchSuppression = null;
    }

    private bool IsHotkeyBox(IInputElement? element)
        => ReferenceEquals(element, RegionHotkeyBox)
            || ReferenceEquals(element, FullScreenHotkeyBox)
            || ReferenceEquals(element, WindowHotkeyBox)
            || ReferenceEquals(element, DelayedHotkeyBox)
            || ReferenceEquals(element, AnnotateHotkeyBox);

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Win;
        }

        return result;
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇自動存檔資料夾",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(SaveDirBox.Text) && Directory.Exists(SaveDirBox.Text))
        {
            dialog.InitialDirectory = SaveDirBox.Text;
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SaveDirBox.Text = dialog.FolderName;
        }
    }

    private void OnBrowseRecordingDirectoryClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇錄影輸出資料夾",
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(RecordingDirBox.Text) && Directory.Exists(RecordingDirBox.Text))
        {
            dialog.InitialDirectory = RecordingDirBox.Text;
        }

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            RecordingDirBox.Text = dialog.FolderName;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => _ = TrySaveSettings();

    private void OnPagePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsHotkeyBox(Keyboard.FocusedElement))
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            e.Handled = true;
            _ = TrySaveSettings();
        }
    }

    private void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
        => OpenFolder(_store.DataRoot);

    private void OnOpenRecordingFolderClick(object sender, RoutedEventArgs e)
        => OpenFolder(ResolveFolder(RecordingDirBox.Text, PathUtil.DefaultRecordingRoot));

    private void OnOpenSaveFolderClick(object sender, RoutedEventArgs e)
        => OpenFolder(ResolveFolder(SaveDirBox.Text, PathUtil.DefaultSaveRoot));

    private static string ResolveFolder(string? configured, string fallback)
    {
        var path = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or Win32Exception)
        {
            SetMessage($"無法開啟資料夾：{exception.Message}", isError: true);
        }
    }

    private bool TrySaveSettings()
    {
        if (!HotkeyGesture.TryParse(RegionHotkeyBox.Text, out var region) || region is null)
        {
            SetMessage(AppStrings.InvalidHotkey, isError: true);
            RegionHotkeyBox.Focus();
            RegionHotkeyBox.SelectAll();
            return false;
        }

        if (!HotkeyGesture.TryParse(FullScreenHotkeyBox.Text, out var fullScreen) || fullScreen is null)
        {
            SetMessage(AppStrings.InvalidHotkey, isError: true);
            FullScreenHotkeyBox.Focus();
            FullScreenHotkeyBox.SelectAll();
            return false;
        }

        if (!HotkeyGesture.TryParse(WindowHotkeyBox.Text, out var window) || window is null)
        {
            SetMessage(AppStrings.InvalidHotkey, isError: true);
            WindowHotkeyBox.Focus();
            WindowHotkeyBox.SelectAll();
            return false;
        }

        if (!HotkeyGesture.TryParse(DelayedHotkeyBox.Text, out var delayed) || delayed is null)
        {
            SetMessage(AppStrings.InvalidHotkey, isError: true);
            DelayedHotkeyBox.Focus();
            DelayedHotkeyBox.SelectAll();
            return false;
        }

        if (!HotkeyGesture.TryParse(AnnotateHotkeyBox.Text, out var annotate) || annotate is null)
        {
            SetMessage(AppStrings.InvalidHotkey, isError: true);
            AnnotateHotkeyBox.Focus();
            AnnotateHotkeyBox.SelectAll();
            return false;
        }

        var gestures = new[] { region, fullScreen, window, delayed, annotate };
        if (gestures.Distinct().Count() != gestures.Length)
        {
            SetMessage("快捷鍵不能重複。", isError: true);
            return false;
        }

        var recordingDirectory = RecordingDirBox.Text.Trim();
        if (!string.IsNullOrEmpty(recordingDirectory))
        {
            try
            {
                recordingDirectory = Path.GetFullPath(recordingDirectory);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                SetMessage("錄影輸出資料夾格式不正確。", isError: true);
                RecordingDirBox.Focus();
                RecordingDirBox.SelectAll();
                return false;
            }
        }

        var settings = _state.Settings.Clone();
        settings.HotkeyRegion = region.DisplayName;
        settings.HotkeyFullScreen = fullScreen.DisplayName;
        settings.HotkeyWindow = window.DisplayName;
        settings.HotkeyDelayed = delayed.DisplayName;
        settings.HotkeyAnnotate = annotate.DisplayName;
        settings.DelaySeconds = DelayCombo.SelectedItem is int delay ? delay : 5;
        settings.AutoCopy = AutoCopyCheck.IsChecked == true;
        settings.PlayCaptureSound = CaptureSoundCheck.IsChecked == true;
        settings.ShowMagnifier = MagnifierCheck.IsChecked == true;
        settings.SaveDirectory = SaveDirBox.Text.Trim();
        settings.SaveAutomatically = SaveAutomaticallyCheck.IsChecked == true;
        settings.DefaultFormat = FormatCombo.SelectedIndex == 1 ? ImageFormat.Jpeg : ImageFormat.Png;
        settings.JpegQuality = Math.Clamp((int)Math.Round(JpegQualitySlider.Value), 1, 100);
        settings.ClipboardHistoryEnabled = ClipboardHistoryCheck.IsChecked == true;
        settings.MaxClipboardItems = MaxClipboardCombo.SelectedItem is int maxClipboard ? maxClipboard : 200;
        settings.MaxCaptureItems = MaxCaptureCombo.SelectedItem is int maxCapture ? maxCapture : 200;
        settings.RetentionDays = RetentionCombo.SelectedItem is int retention ? retention : 30;
        settings.Theme = GetSelectedTheme();
        settings.StartWithWindows = StartupCheck.IsChecked == true;
        settings.Annotation.ClickThrough = AnnotateClickThroughCheck.IsChecked == true;
        settings.Annotation.Backdrop = AnnotateBackdropCombo.SelectedIndex is >= 0 and <= 2
            ? (AnnotationBackdrop)AnnotateBackdropCombo.SelectedIndex
            : AnnotationBackdrop.Screen;
        settings.Recording.OutputDirectory = recordingDirectory;
        settings.Recording.FrameRate = RecordingFrameRateCombo.SelectedItem is int frameRate ? frameRate : 30;
        settings.Recording.Bitrate = RecordingBitrateCombo.SelectedValue is int bitrate ? bitrate : 8_000_000;
        settings.Recording.IncludeCursor = RecordingCursorCheck.IsChecked == true;
        settings.Recording.IncludeSystemAudio = RecordingSystemAudioCheck.IsChecked == true;
        settings.Recording.DelaySeconds = RecordingDelayCombo.SelectedItem is int recordingDelay ? recordingDelay : 0;

        _isLoading = true;
        RegionHotkeyBox.Text = region.DisplayName;
        FullScreenHotkeyBox.Text = fullScreen.DisplayName;
        WindowHotkeyBox.Text = window.DisplayName;
        DelayedHotkeyBox.Text = delayed.DisplayName;
        AnnotateHotkeyBox.Text = annotate.DisplayName;
        _isLoading = false;

        _state.Save(settings);
        _isDirty = false;
        _baseline = CaptureSnapshot();
        UpdateDirtyState();

        var failures = App.Current.ApplyHotkeys();
        if (failures.Count > 0)
        {
            SetMessage("已儲存，但以下快捷鍵無法註冊（可能被其他程式佔用）：" + string.Join("、", failures), isError: true);
            return true;
        }

        SetMessage(AppStrings.SettingsSaved, isError: false);
        _ = TrimAsync(settings);
        return true;
    }

    private async Task TrimAsync(AppSettings settings)
    {
        try
        {
            await _store.TrimCapturesAsync(settings.MaxCaptureItems, settings.RetentionDays);
            await _store.TrimClipboardItemsAsync(settings.MaxClipboardItems, settings.RetentionDays);
        }
        catch (Exception)
        {
        }
    }

    private void OnJpegQualityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (JpegQualityText is not null)
        {
            JpegQualityText.Text = $"{Math.Round(e.NewValue):0}%";
        }
    }

    private bool HasChanges()
        => _baseline is not null && CaptureSnapshot() != _baseline;

    private SettingsSnapshot CaptureSnapshot() => new(
        NormalizeHotkey(RegionHotkeyBox.Text),
        NormalizeHotkey(FullScreenHotkeyBox.Text),
        NormalizeHotkey(WindowHotkeyBox.Text),
        NormalizeHotkey(DelayedHotkeyBox.Text),
        NormalizeHotkey(AnnotateHotkeyBox.Text),
        DelayCombo.SelectedItem is int delay ? delay : 5,
        AutoCopyCheck.IsChecked == true,
        CaptureSoundCheck.IsChecked == true,
        MagnifierCheck.IsChecked == true,
        (SaveDirBox.Text ?? string.Empty).Trim(),
        SaveAutomaticallyCheck.IsChecked == true,
        FormatCombo.SelectedIndex == 1 ? ImageFormat.Jpeg : ImageFormat.Png,
        Math.Clamp((int)Math.Round(JpegQualitySlider.Value), 1, 100),
        ClipboardHistoryCheck.IsChecked == true,
        MaxClipboardCombo.SelectedItem is int maxClipboard ? maxClipboard : 200,
        MaxCaptureCombo.SelectedItem is int maxCapture ? maxCapture : 200,
        RetentionCombo.SelectedItem is int retention ? retention : 30,
        GetSelectedTheme(),
        StartupCheck.IsChecked == true,
        AnnotateClickThroughCheck.IsChecked == true,
        AnnotateBackdropCombo.SelectedIndex is >= 0 and <= 2
            ? (AnnotationBackdrop)AnnotateBackdropCombo.SelectedIndex
            : AnnotationBackdrop.Screen,
        (RecordingDirBox.Text ?? string.Empty).Trim(),
        RecordingFrameRateCombo.SelectedItem is int frameRate ? frameRate : 30,
        RecordingBitrateCombo.SelectedValue is int bitrate ? bitrate : 8_000_000,
        RecordingCursorCheck.IsChecked == true,
        RecordingSystemAudioCheck.IsChecked == true,
        RecordingDelayCombo.SelectedItem is int recordingDelay ? recordingDelay : 0);

    private static string NormalizeHotkey(string text)
        => HotkeyGesture.TryParse(text, out var gesture) && gesture is not null
            ? gesture.DisplayName
            : "\0" + text;

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoading)
        {
            App.Current.ApplyTheme(GetSelectedTheme());
        }
    }

    private AppTheme GetSelectedTheme()
        => ThemeCombo.SelectedItem is ThemeOption option ? option.Value : AppTheme.System;

    private void MarkDirty()
    {
        if (_isLoading)
        {
            return;
        }

        _isDirty = HasChanges();
        MessageText.Text = string.Empty;
        UpdateDirtyState();
    }

    private void UpdateDirtyState()
    {
        DirtyText.Visibility = _isDirty ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _isDirty;
    }

    private void SetMessage(string text, bool isError)
    {
        MessageText.Text = text;
        var key = isError ? "DangerBrush" : "TextSecondaryBrush";
        MessageText.Foreground = (Brush)FindResource(key);
    }

    private sealed record SettingsSnapshot(
        string HotkeyRegion,
        string HotkeyFullScreen,
        string HotkeyWindow,
        string HotkeyDelayed,
        string HotkeyAnnotate,
        int DelaySeconds,
        bool AutoCopy,
        bool PlayCaptureSound,
        bool ShowMagnifier,
        string SaveDirectory,
        bool SaveAutomatically,
        ImageFormat DefaultFormat,
        int JpegQuality,
        bool ClipboardHistoryEnabled,
        int MaxClipboardItems,
        int MaxCaptureItems,
        int RetentionDays,
        AppTheme Theme,
        bool StartWithWindows,
        bool AnnotateClickThrough,
        AnnotationBackdrop AnnotateBackdrop,
        string RecordingDirectory,
        int RecordingFrameRate,
        int RecordingBitrate,
        bool RecordingIncludeCursor,
        bool RecordingIncludeSystemAudio,
        int RecordingDelaySeconds);

    private sealed record ThemeOption(AppTheme Value, string DisplayName);

    private sealed record BitrateOption(int Value, string DisplayName);
}

