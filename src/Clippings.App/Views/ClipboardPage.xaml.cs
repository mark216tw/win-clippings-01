using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clippings.App.Helpers;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Imaging;
using Microsoft.Extensions.Logging;

namespace Clippings.App.Views;

public partial class ClipboardPage : UserControl
{
    private readonly IClippingsStore _store;
    private readonly AppState _state;
    private readonly ILogger<ClipboardPage> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public ClipboardPage(IClippingsStore store, AppState state, ILogger<ClipboardPage> logger)
    {
        InitializeComponent();

        _store = store;
        _state = state;
        _logger = logger;
    }

    public async Task ReloadAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            var items = await _store.GetClipboardItemsAsync(
                null,
                ClipboardItemKind.Image,
                Math.Max(50, _state.Settings.MaxClipboardItems));
            var visibleItems = PinnedFilter.IsChecked == true
                ? items.Where(item => item.IsPinned).ToList()
                : items.ToList();
            ItemsList.ItemsSource = visibleItems;
            CountText.Text = $"{visibleItems.Count} 筆";
            EmptyState.Text = AppStrings.EmptyClipboardImages;
            EmptyState.Visibility = visibleItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ClearButton.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "讀取剪貼簿歷史失敗。");
            StatusText.Text = "讀取失敗：" + exception.Message;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static ClipboardItem? GetItem(object sender)
        => (sender as FrameworkElement)?.DataContext as ClipboardItem;

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private void OnFilterChanged(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item)
        {
            await CopyItemAsync(item);
        }
    }

    private async void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is ClipboardItem item)
        {
            await CopyItemAsync(item);
        }
    }

    private async void OnItemsKeyDown(object sender, KeyEventArgs e)
    {
        if (ItemsList.SelectedItem is not ClipboardItem item)
        {
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            e.Handled = true;
            await CopyItemAsync(item);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await CopyItemAsync(item);
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            await DeleteItemAsync(item);
        }
        else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.P)
        {
            e.Handled = true;
            await TogglePinAsync(item);
        }
    }

    private async Task CopyItemAsync(ClipboardItem item)
    {
        if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
        {
            StatusText.Text = AppStrings.CopyImageMissingFile;
            return;
        }

        try
        {
            var source = await Task.Run(() =>
            {
                using var bitmap = ImageCodec.Load(item.FilePath!);
                return BitmapSourceHelper.FromSKBitmap(bitmap);
            });

            StatusText.Text = ClipboardHelper.TrySetImage(source)
                ? AppStrings.ClipboardCopied
                : AppStrings.ClipboardCopyFailed;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "複製剪貼簿項目失敗。");
            StatusText.Text = "複製失敗：" + exception.Message;
        }
    }

    private void OnRevealClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        StatusText.Text = FileActionHelper.TryRevealInExplorer(item.FilePath, out var error)
            ? AppStrings.RevealedInExplorer
            : error ?? AppStrings.FileMissing;
    }

    private void OnCopyPathClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
        {
            StatusText.Text = AppStrings.FileMissing;
            return;
        }

        StatusText.Text = FileActionHelper.TryCopyText(item.FilePath)
            ? AppStrings.FilePathCopied
            : AppStrings.FilePathCopyFailed;
    }

    private async void OnAddToCaptureClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { Kind: ClipboardItemKind.Image } item)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.FilePath) || !File.Exists(item.FilePath))
        {
            StatusText.Text = AppStrings.AddToCaptureMissingFile;
            return;
        }

        try
        {
            var path = item.FilePath;
            var (imageBytes, thumbnailBytes, width, height) = await Task.Run(() =>
            {
                using var bitmap = ImageCodec.Load(path);
                return (
                    ImageCodec.Encode(bitmap, ImageFormat.Png),
                    ImageCodec.EncodeThumbnailJpeg(bitmap),
                    bitmap.Width,
                    bitmap.Height);
            });

            var capture = new CaptureItem
            {
                CreatedAt = DateTimeOffset.Now,
                Width = width,
                Height = height,
                Source = AppStrings.CaptureSourceFromClipboard,
            };

            await _store.AddCaptureAsync(capture, imageBytes, "png", thumbnailBytes);
            (Window.GetWindow(this) as MainWindow)?.NavigateTo(0);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "加入最近擷取失敗。");
            StatusText.Text = AppStrings.FormatAddToCaptureFailed(exception.Message);
        }
    }

    private async void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item)
        {
            await TogglePinAsync(item);
        }
    }

    private async Task TogglePinAsync(ClipboardItem item)
    {
        try
        {
            await _store.SetClipboardPinnedAsync(item.Id, !item.IsPinned);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "更新釘選狀態失敗。");
            StatusText.Text = "更新失敗：" + exception.Message;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is { } item)
        {
            await DeleteItemAsync(item);
        }
    }

    private async Task DeleteItemAsync(ClipboardItem item)
    {
        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(
            owner,
            AppStrings.DeleteConfirmMessage,
            AppStrings.DeleteConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _store.DeleteClipboardItemAsync(item.Id);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "刪除剪貼簿項目失敗。");
            StatusText.Text = "刪除失敗：" + exception.Message;
        }
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(
            owner,
            AppStrings.ClearClipboardConfirmMessage,
            AppStrings.AppName,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _store.ClearClipboardItemsAsync();
            await ReloadAsync();
            StatusText.Text = "已清除未釘選的項目。";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "清除剪貼簿歷史失敗。");
            StatusText.Text = "清除失敗：" + exception.Message;
        }
    }
}
