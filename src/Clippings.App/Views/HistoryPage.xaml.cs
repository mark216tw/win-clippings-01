using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Clippings.App.Helpers;
using Clippings.App.Services;
using Clippings.App.Strings;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SkiaSharp;

namespace Clippings.App.Views;

public partial class HistoryPage : UserControl
{
    private readonly IClippingsStore _store;
    private readonly AppState _state;
    private readonly ILogger<HistoryPage> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DispatcherTimer _searchTimer;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    public HistoryPage(IClippingsStore store, AppState state, ILogger<HistoryPage> logger, ILoggerFactory loggerFactory)
    {
        InitializeComponent();

        _store = store;
        _state = state;
        _logger = logger;
        _loggerFactory = loggerFactory;

        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            _ = ReloadAsync();
        };
    }

    public async Task ReloadAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            LoadingState.Visibility = Visibility.Visible;
            ErrorState.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            var items = await _store.GetCapturesAsync(
                SearchBox.Text,
                FavoriteFilter.IsChecked == true,
                Math.Max(50, _state.Settings.MaxCaptureItems));

            ItemsList.ItemsSource = items;
            CountText.Text = $"{items.Count} 張";
            var libraryEmpty = items.Count == 0 &&
                string.IsNullOrWhiteSpace(SearchBox.Text) &&
                FavoriteFilter.IsChecked != true;
            EmptyState.Text = string.IsNullOrWhiteSpace(SearchBox.Text) && FavoriteFilter.IsChecked != true
                ? AppStrings.EmptyCaptureHistory
                : AppStrings.EmptySearchResult;
            EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DeleteAllButton.Visibility = libraryEmpty ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "讀取擷取歷史失敗。");
            ErrorState.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            StatusText.Text = string.Empty;
        }
        finally
        {
            LoadingState.Visibility = Visibility.Collapsed;
            _reloadLock.Release();
        }
    }

    private static CaptureItem? GetItem(object sender)
        => (sender as FrameworkElement)?.DataContext as CaptureItem;

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var hasText = !string.IsNullOrWhiteSpace(SearchBox.Text);
        SearchHint.Visibility = hasText || SearchBox.IsKeyboardFocusWithin
            ? Visibility.Collapsed
            : Visibility.Visible;
        ClearSearchButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OnSearchGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchHint.Visibility = Visibility.Collapsed;
        if (SearchBox.Text.Length == 0)
        {
            SearchBox.CaretIndex = 0;
        }
    }

    private void OnSearchLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchHint.Visibility = string.IsNullOrWhiteSpace(SearchBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        FocusSearch();
    }

    private void OnRetryClick(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private void OnFilterChanged(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private async void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        try
        {
            var source = await Task.Run(() =>
            {
                using var bitmap = ImageCodec.Load(item.FilePath);
                return BitmapSourceHelper.FromSKBitmap(bitmap);
            });

            StatusText.Text = ClipboardHelper.TrySetImage(source)
                ? AppStrings.ClipboardCopied
                : AppStrings.ClipboardCopyFailed;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "複製擷取影像失敗。");
            StatusText.Text = "複製失敗：" + exception.Message;
        }
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        await OpenEditorAsync(item);
    }

    private async Task OpenEditorAsync(CaptureItem item)
    {
        SKBitmap? bitmap = null;
        try
        {
            bitmap = await Task.Run(() => ImageCodec.Load(item.FilePath));
            var editor = new EditorWindow(bitmap, _state, _store, _loggerFactory, item, canDiscard: false);
            bitmap = null;
            editor.Show();
            editor.Activate();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "開啟編輯器失敗。");
            StatusText.Text = "無法開啟影像：" + exception.Message;
        }
        finally
        {
            bitmap?.Dispose();
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG 影像 (*.png)|*.png|JPEG 影像 (*.jpg;*.jpeg)|*.jpg;*.jpeg",
            FileName = Path.GetFileNameWithoutExtension(item.FilePath),
            DefaultExt = ".png",
            AddExtension = true,
            InitialDirectory = ResolveSaveDirectory(),
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            var format = dialog.FilterIndex == 2 ? ImageFormat.Jpeg : ImageFormat.Png;
            var targetPath = Path.ChangeExtension(dialog.FileName, format == ImageFormat.Jpeg ? ".jpg" : ".png");

            await Task.Run(() =>
            {
                if (format == ImageFormat.Png)
                {
                    File.Copy(item.FilePath, targetPath, overwrite: true);
                    return;
                }

                using var bitmap = ImageCodec.Load(item.FilePath);
                File.WriteAllBytes(targetPath, ImageCodec.Encode(bitmap, ImageFormat.Jpeg, 92));
            });

            StatusText.Text = $"{AppStrings.Saved} {targetPath}";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "另存擷取影像失敗。");
            StatusText.Text = "儲存失敗：" + exception.Message;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

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
            await _store.DeleteCaptureAsync(item.Id);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "刪除擷取影像失敗。");
            StatusText.Text = "刪除失敗：" + exception.Message;
        }
    }

    private async void OnDeleteAllClick(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(
            owner,
            AppStrings.DeleteAllCapturesConfirmMessage,
            AppStrings.DeleteAllCapturesConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var deleted = await _store.DeleteNonFavoriteCapturesAsync();
            await ReloadAsync();
            StatusText.Text = deleted == 0
                ? AppStrings.DeleteAllCapturesNone
                : AppStrings.FormatDeleteAllCapturesResult(deleted);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "刪除全部擷取失敗。");
            StatusText.Text = "刪除失敗：" + exception.Message;
        }
    }

    private async void OnFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is not { } item)
        {
            return;
        }

        try
        {
            await _store.SetCaptureFavoriteAsync(item.Id, !item.IsFavorite);
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "更新收藏狀態失敗。");
            StatusText.Text = "更新失敗：" + exception.Message;
        }
    }

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsList.SelectedItem is CaptureItem item)
        {
            _ = OpenEditorAsync(item);
        }
    }

    private string ResolveSaveDirectory()
    {
        var configured = _state.Settings.SaveDirectory;
        return string.IsNullOrWhiteSpace(configured)
            ? Clippings.Core.Helpers.PathUtil.DefaultSaveRoot
            : configured;
    }
}


