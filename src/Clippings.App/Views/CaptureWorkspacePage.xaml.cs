using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Clippings.App.Helpers;
using Clippings.App.Models;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SkiaSharp;

namespace Clippings.App.Views;

public partial class CaptureWorkspacePage : UserControl
{
    private const int SaveDebounceMilliseconds = 800;
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(6);

    private readonly AppState _state;
    private readonly CaptureWorkspaceService _workspace;
    private readonly CaptureCoordinator _coordinator;
    private readonly WorkspaceTabStrip _tabStrip;
    private readonly ILogger<CaptureWorkspacePage> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private EditorView? _editor;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly SemaphoreSlim _closeGate = new(1, 1);
    private readonly Task _initialization;
    private List<PendingClosedTab> _pendingClosedTabs = [];
    private WorkspaceTab? _selectedTab;
    private WorkspaceTab? _suspendedTab;
    private bool _pendingCloseAll;
    private bool _isCommittingPendingClose;
    private bool _suppressSelection;
    private bool _initializing = true;
    private int _selectionVersion;

    public CaptureWorkspacePage(
        AppState state,
        CaptureWorkspaceService workspace,
        CaptureCoordinator coordinator,
        ILoggerFactory loggerFactory)
    {
        InitializeComponent();

        _state = state;
        _workspace = workspace;
        _coordinator = coordinator;
        _logger = loggerFactory.CreateLogger<CaptureWorkspacePage>();
        _loggerFactory = loggerFactory;
        _tabStrip = new WorkspaceTabStrip();
        _tabStrip.ItemsSource = _workspace.Tabs;
        _tabStrip.TabSelectionChanged += OnTabSelectionChanged;
        _tabStrip.TabCloseRequested += OnTabCloseRequested;
        _tabStrip.CloseAllRequested += OnTabStripCloseAllRequested;

        _workspace.Tabs.CollectionChanged += OnWorkspaceTabsChanged;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaveDebounceMilliseconds) };
        _saveTimer.Tick += OnSaveTimerTick;
        _toastTimer = new DispatcherTimer { Interval = ToastDuration };
        _toastTimer.Tick += OnToastTimerTick;

        UpdateTabCount();
        UpdateEmptyState();
        UpdateEditorInfo();
        _initialization = InitializeAsync();
    }

    private EditorView EnsureEditor()
    {
        if (_editor is not null)
        {
            return _editor;
        }

        var editor = new EditorView(_loggerFactory);
        editor.SetTabBar(_tabStrip);
        editor.DocumentChanged += OnEditorDocumentChanged;
        editor.SaveAsRequested += OnEditorSaveAsRequested;
        editor.OpenImageFromClipboardRequested += OnEditorOpenImageFromClipboardRequested;
        editor.OpenImageFileRequested += OnEditorOpenImageFileRequested;
        editor.ViewInfoChanged += OnEditorViewInfoChanged;
        EditorHost.Content = editor;
        _editor = editor;
        return editor;
    }

    public async Task ShowTabAsync(WorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        await _initialization;
        _suspendedTab = null;
        if (!_workspace.Tabs.Contains(tab))
        {
            return;
        }

        await SelectTabAsync(tab);
    }

    public async Task ActivateAsync()
    {
        await _initialization;
        UpdateTabCount();
        UpdateEmptyState();

        if (_selectedTab is null && _workspace.Tabs.Count > 0)
        {
            var target = _suspendedTab is not null && _workspace.Tabs.Contains(_suspendedTab)
                ? _suspendedTab
                : _workspace.Tabs[^1];
            _suspendedTab = null;
            await SelectTabAsync(target);
        }
    }

    public void SuspendForTray()
    {
        var editor = _editor;
        if (editor is null || editor.IsDirty)
        {
            return;
        }

        _saveTimer.Stop();
        _suspendedTab = _selectedTab;
        _selectedTab = null;
        _selectionVersion++;

        editor.DocumentChanged -= OnEditorDocumentChanged;
        editor.SaveAsRequested -= OnEditorSaveAsRequested;
        editor.OpenImageFromClipboardRequested -= OnEditorOpenImageFromClipboardRequested;
        editor.OpenImageFileRequested -= OnEditorOpenImageFileRequested;
        editor.ViewInfoChanged -= OnEditorViewInfoChanged;
        editor.ReleaseResources();

        EditorHost.Content = null;
        _editor = null;
        UpdateEditorInfo();
    }

    private void OnWorkspaceTabsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateTabCount();
        UpdateEmptyState();
    }

    public async Task FlushPendingAsync()
    {
        _saveTimer.Stop();
        await SaveTabAsync(_selectedTab, force: false);
        await CommitPendingCloseAsync();
    }

    public bool HandleEditorKey(KeyEventArgs e) => _editor?.HandleEditorKey(e) == true;

    private async Task InitializeAsync()
    {
        await _workspace.LoadAsync();
        UpdateTabCount();
        _initializing = false;

        var last = _workspace.Tabs.LastOrDefault();
        if (last is null)
        {
            UpdateEmptyState();
            return;
        }

        await SelectTabAsync(last);
    }

    private void OnEditorDocumentChanged(object? sender, EventArgs e)
    {
        if (_selectedTab is not null)
        {
            _selectedTab.IsSaved = false;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void OnEditorSaveAsRequested(object? sender, EventArgs e) => _ = SaveAsAsync();

    private async void OnSaveTimerTick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        await SaveTabAsync(_selectedTab, force: false);
    }

    private async void OnTabSelectionChanged(object? sender, EventArgs e)
    {
        if (_suppressSelection || _initializing)
        {
            return;
        }

        await SelectTabAsync(_tabStrip.SelectedTab);
    }

    private async Task SelectTabAsync(WorkspaceTab? target)
    {
        var hasImage = _editor?.HasImage ?? false;
        if (ReferenceEquals(target, _selectedTab) &&
            ReferenceEquals(target, _tabStrip.SelectedTab) &&
            (target is null ? !hasImage : hasImage))
        {
            UpdateTabCount();
            UpdateEmptyState();
            return;
        }

        var version = ++_selectionVersion;
        _saveTimer.Stop();
        UpdateTabCount();

        var previous = _selectedTab;
        if (previous is not null && !ReferenceEquals(previous, target))
        {
            var saved = await SaveTabAsync(previous, force: false);
            if (!saved)
            {
                _suppressSelection = true;
                _tabStrip.SelectedTab = previous;
                _suppressSelection = false;
                return;
            }
        }

        if (version != _selectionVersion)
        {
            return;
        }

        _selectedTab = target;
        if (!ReferenceEquals(_tabStrip.SelectedTab, target))
        {
            _suppressSelection = true;
            _tabStrip.SelectedTab = target;
            _suppressSelection = false;
        }

        if (target is null)
        {
            _editor?.ClearImage();
            UpdateEmptyState();
            return;
        }

        try
        {
            var bitmap = await LoadTabBitmapAsync(target);
            if (version != _selectionVersion)
            {
                bitmap?.Dispose();
                return;
            }

            if (bitmap is null)
            {
                await CloseMissingTabAsync(target);
                return;
            }

            EnsureEditor().LoadImage(bitmap);
            StatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "載入擷圖頁籤失敗。");
            StatusText.Text = "無法載入圖片：" + exception.Message;
            await CloseMissingTabAsync(target);
        }

        UpdateEmptyState();
    }

    private static async Task<SKBitmap?> LoadTabBitmapAsync(WorkspaceTab tab)
    {
        if (tab.HasTempFile && File.Exists(tab.TempPath))
        {
            var path = tab.TempPath!;
            return await Task.Run(() => ImageCodec.Load(path));
        }

        if (tab.MemoryImage is { Length: > 0 } bytes)
        {
            return await Task.Run(() => ImageCodec.Decode(bytes));
        }

        return null;
    }

    private async Task<bool> SaveTabAsync(WorkspaceTab? tab, bool force)
    {
        var editor = _editor;
        if (tab is null || editor is null || !editor.HasImage || (!force && !editor.IsDirty))
        {
            return true;
        }

        await _saveGate.WaitAsync();
        try
        {
            editor = _editor;
            if (tab is null || editor is null || !editor.HasImage || (!force && !editor.IsDirty))
            {
                return true;
            }

            editor.FlushPendingEdits();
            var version = editor.EditVersion;
            using var flattened = editor.CreateFlattenedBitmap(SKColors.Transparent);
            var width = flattened.Width;
            var height = flattened.Height;
            var bytes = await Task.Run(() => ImageCodec.Encode(flattened, ImageFormat.Png));
            var saved = await _workspace.UpdateTempAsync(tab, bytes, width, height);

            if (!saved)
            {
                StatusText.Text = AppStrings.FormatWorkspaceTempSaveFailed("請確認資料夾權限或磁碟空間。");
                return false;
            }

            if (ReferenceEquals(tab, _selectedTab) && version == editor.EditVersion)
            {
                editor.MarkClean();
            }

            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "自動暫存擷圖失敗。");
            StatusText.Text = AppStrings.FormatWorkspaceTempSaveFailed(exception.Message);
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async void OnTabCloseRequested(object? sender, WorkspaceTab tab)
        => await CloseTabAsync(tab);

    private async Task CloseTabAsync(WorkspaceTab tab, bool allowRestore = true)
    {
        await _closeGate.WaitAsync();
        try
        {
            await CloseTabCoreAsync(tab, allowRestore);
        }
        finally
        {
            _closeGate.Release();
        }
    }

    private async Task CloseTabCoreAsync(WorkspaceTab tab, bool allowRestore)
    {
        if (allowRestore)
        {
            await CommitPendingCloseAsync();
        }

        _selectionVersion++;
        var index = _workspace.Tabs.IndexOf(tab);
        var wasSelected = ReferenceEquals(tab, _selectedTab);
        if (wasSelected)
        {
            if (allowRestore)
            {
                _saveTimer.Stop();
                await SaveTabAsync(tab, force: false);
            }

            _selectedTab = null;
            _editor?.ClearImage();
        }

        _saveTimer.Stop();
        _suppressSelection = true;
        await _saveGate.WaitAsync();
        try
        {
            if (allowRestore)
            {
                _workspace.DetachTab(tab);
                _pendingClosedTabs = [new PendingClosedTab(tab, index, wasSelected)];
                _pendingCloseAll = false;
            }
            else
            {
                await _workspace.DeleteTabAsync(tab);
            }
        }
        finally
        {
            _saveGate.Release();
            _suppressSelection = false;
        }

        UpdateTabCount();
        if (allowRestore)
        {
            ShowToast(AppStrings.FormatWorkspaceTabClosed(tab.Title), AppStrings.WorkspaceRestoreTab);
        }

        if (!wasSelected)
        {
            UpdateEmptyState();
            return;
        }

        var next = index >= 0 && _workspace.Tabs.Count > 0
            ? _workspace.Tabs[Math.Min(index, _workspace.Tabs.Count - 1)]
            : null;
        await SelectTabAsync(next);
    }

    private async Task CloseMissingTabAsync(WorkspaceTab tab)
    {
        StatusText.Text = AppStrings.WorkspaceImageMissing;
        await CloseTabCoreAsync(tab, allowRestore: false);
    }

    private async void OnTabStripCloseAllRequested(object? sender, EventArgs e)
    {
        await _initialization;
        await _closeGate.WaitAsync();
        try
        {
            await CloseAllTabsCoreAsync();
        }
        finally
        {
            _closeGate.Release();
        }
    }

    private async Task CloseAllTabsCoreAsync()
    {
        await CommitPendingCloseAsync();

        _selectionVersion++;
        _saveTimer.Stop();
        if (_selectedTab is { } selected)
        {
            await SaveTabAsync(selected, force: false);
        }

        _selectedTab = null;
        _editor?.ClearImage();

        var detached = _workspace.Tabs
            .Select((tab, index) => new PendingClosedTab(tab, index, WasSelected: false))
            .ToList();

        _suppressSelection = true;
        await _saveGate.WaitAsync();
        try
        {
            _workspace.DetachAll();
            _pendingClosedTabs = detached;
            _pendingCloseAll = true;
        }
        finally
        {
            _saveGate.Release();
            _suppressSelection = false;
        }

        StatusText.Text = string.Empty;
        UpdateTabCount();
        UpdateEmptyState();

        if (detached.Count > 0)
        {
            ShowToast(
                AppStrings.FormatWorkspaceAllTabsClosed(detached.Count),
                AppStrings.WorkspaceRestoreAll);
        }
    }

    private void ShowToast(string message, string actionText)
    {
        ToastText.Text = message;
        ToastActionButton.Content = actionText;
        AutomationProperties.SetName(ToastActionButton, actionText);
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer.Stop();
        Toast.Visibility = Visibility.Collapsed;
    }

    private async Task CommitPendingCloseAsync()
    {
        HideToast();
        if (_isCommittingPendingClose || _pendingClosedTabs.Count == 0)
        {
            return;
        }

        _isCommittingPendingClose = true;
        var pending = _pendingClosedTabs;
        _pendingClosedTabs = [];
        _pendingCloseAll = false;
        try
        {
            foreach (var pendingTab in pending)
            {
                await _workspace.DeleteDetachedTabAsync(pendingTab.Tab);
            }
        }
        finally
        {
            _isCommittingPendingClose = false;
        }
    }

    private async Task RestorePendingCloseAsync()
    {
        HideToast();
        var pending = _pendingClosedTabs;
        var closeAll = _pendingCloseAll;
        _pendingClosedTabs = [];
        _pendingCloseAll = false;
        if (pending.Count == 0)
        {
            return;
        }

        if (_workspace.Tabs.Count + pending.Count > CaptureWorkspaceLimits.MaxTabs)
        {
            StatusText.Text = AppStrings.WorkspaceRestoreLimitReached;
            foreach (var pendingTab in pending)
            {
                await _workspace.DeleteDetachedTabAsync(pendingTab.Tab);
            }

            return;
        }

        _suppressSelection = true;
        try
        {
            foreach (var pendingTab in pending.OrderBy(item => item.Index))
            {
                _workspace.TryRestoreTab(pendingTab.Tab, pendingTab.Index);
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        UpdateTabCount();
        UpdateEmptyState();

        var target = closeAll
            ? _workspace.Tabs.LastOrDefault()
            : pending[0].WasSelected || _selectedTab is null
                ? pending[0].Tab
                : _selectedTab;
        await SelectTabAsync(target);
    }

    private async Task RunToastActionAsync(Func<Task> action)
    {
        await _closeGate.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _closeGate.Release();
        }
    }

    private async void OnToastTimerTick(object? sender, EventArgs e)
        => await RunToastActionAsync(CommitPendingCloseAsync);

    private async void OnToastActionClick(object sender, RoutedEventArgs e)
        => await RunToastActionAsync(RestorePendingCloseAsync);

    private async void OnToastCloseClick(object sender, RoutedEventArgs e)
        => await RunToastActionAsync(CommitPendingCloseAsync);

    private async Task SaveAsAsync()
    {
        var editor = _editor;
        if (_selectedTab is not { } tab || editor is null || !editor.HasImage)
        {
            return;
        }

        _saveTimer.Stop();
        await _saveGate.WaitAsync();
        try
        {
            var settings = _state.Settings;
            var defaultJpeg = settings.DefaultFormat == ImageFormat.Jpeg;
            var extension = defaultJpeg ? ".jpg" : ".png";
            var dialog = new SaveFileDialog
            {
                Filter = "PNG 影像 (*.png)|*.png|JPEG 影像 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
                FilterIndex = defaultJpeg ? 2 : 1,
                FileName = $"Clippings_{DateTime.Now:yyyyMMdd_HHmmss}{extension}",
                DefaultExt = extension,
                AddExtension = true,
                InitialDirectory = string.IsNullOrWhiteSpace(settings.SaveDirectory)
                    ? PathUtil.DefaultSaveRoot
                    : settings.SaveDirectory,
            };

            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            editor.FlushPendingEdits();
            var version = editor.EditVersion;
            using var flattened = editor.CreateFlattenedBitmap(SKColors.White);
            var width = flattened.Width;
            var height = flattened.Height;
            var format = dialog.FilterIndex == 2 ? ImageFormat.Jpeg : ImageFormat.Png;
            var pngBytes = await Task.Run(() => ImageCodec.Encode(flattened, ImageFormat.Png));
            var outputBytes = format == ImageFormat.Png
                ? pngBytes
                : await Task.Run(() => ImageCodec.Encode(flattened, format, settings.JpegQuality));

            await File.WriteAllBytesAsync(dialog.FileName, outputBytes);
            await _workspace.DeleteTempAfterSaveAsync(tab);
            tab.MemoryImage = pngBytes;
            tab.IsSaved = version == editor.EditVersion;
            if (version == editor.EditVersion)
            {
                editor.MarkClean();
            }
            else
            {
                _saveTimer.Stop();
                _saveTimer.Start();
            }

            UpdateTabCount();
            StatusText.Text = $"{AppStrings.Saved} {dialog.FileName}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "另存新檔失敗。");
            StatusText.Text = "儲存失敗：" + exception.Message;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void OnCaptureRegionClick(object sender, RoutedEventArgs e)
        => _ = _coordinator.CaptureRegionAsync(CaptureInvocationSource.QuickCapture);

    private void OnCaptureFullScreenClick(object sender, RoutedEventArgs e)
        => _ = _coordinator.CaptureFullScreenAsync(CaptureInvocationSource.QuickCapture);

    private void OnEmptyOpenImageClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private async void OnEmptyOpenFromClipboardClick(object sender, RoutedEventArgs e)
        => await OpenImageFromClipboardAsync();

    private async void OnEmptyOpenFileClick(object sender, RoutedEventArgs e)
        => await OpenImageFromFileAsync();

    private async void OnEditorOpenImageFromClipboardRequested(object? sender, EventArgs e)
        => await OpenImageFromClipboardAsync();

    private async void OnEditorOpenImageFileRequested(object? sender, EventArgs e)
        => await OpenImageFromFileAsync();

    private async Task OpenImageFromClipboardAsync()
    {
        await _initialization;
        if (!ClipboardHelper.TryGetImage(out var source) || source is null)
        {
            StatusText.Text = AppStrings.OpenImageClipboardEmpty;
            return;
        }

        SKBitmap? bitmap = null;
        try
        {
            bitmap = BitmapSourceHelper.ToSKBitmap(source);
            await AddOpenedImageAsync(bitmap, title: null);
            bitmap = null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "從剪貼簿開啟圖檔失敗。");
            StatusText.Text = AppStrings.FormatOpenImageFailed(exception.Message);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private async Task OpenImageFromFileAsync()
    {
        await _initialization;
        var dialog = new OpenFileDialog
        {
            Filter = AppStrings.OpenImageFileFilter,
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        SKBitmap? bitmap = null;
        try
        {
            var path = dialog.FileName;
            var title = Path.GetFileNameWithoutExtension(path);
            bitmap = await Task.Run(() => ImageCodec.Load(path));
            await AddOpenedImageAsync(bitmap, title);
            bitmap = null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "開啟圖檔失敗。");
            StatusText.Text = AppStrings.FormatOpenImageFailed(exception.Message);
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private async Task AddOpenedImageAsync(SKBitmap bitmap, string? title)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var bytes = await Task.Run(() => ImageCodec.Encode(bitmap, ImageFormat.Png));
        var result = await _workspace.AddTabAsync(bytes, width, height, title);

        if (result.Tab is { } tab)
        {
            await SelectTabAsync(tab);
            StatusText.Text = string.Empty;
            return;
        }

        StatusText.Text = result.LimitReached
            ? AppStrings.WorkspaceTabLimitReached
            : AppStrings.FormatWorkspaceTempSaveFailed("請確認資料夾權限或磁碟空間。");
    }

    private void OnEditorViewInfoChanged(object? sender, EventArgs e) => UpdateEditorInfo();

    private void UpdateEditorInfo()
    {
        var editor = _editor;
        var hasImage = editor?.HasImage ?? false;
        HeaderImageSizeText.Text = hasImage ? editor!.ImageSizeDisplay : "—";
        HeaderZoomText.Text = hasImage ? editor!.ZoomDisplay : "—";
        HeaderZoomText.ToolTip = hasImage ? editor!.ZoomToolTip : null;
        ActualPixelsButton.IsEnabled = hasImage;
    }

    private void OnActualPixelsClick(object sender, RoutedEventArgs e) => _editor?.ZoomToActualPixels();

    private void UpdateTabCount()
    {
        CountText.Text = $"{_workspace.Tabs.Count} / {CaptureWorkspaceLimits.MaxTabs}";
    }

    private void UpdateEmptyState()
    {
        var hasTabs = _workspace.Tabs.Count > 0;
        EditorHost.Visibility = hasTabs ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasTabs ? Visibility.Collapsed : Visibility.Visible;
        _tabStrip.SetCloseAllEnabled(hasTabs);
    }

    private readonly record struct PendingClosedTab(WorkspaceTab Tab, int Index, bool WasSelected);
}
