using System.Media;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clippings.App.Helpers;
using Clippings.App.Strings;
using Clippings.App.Views;
using Clippings.Core.Editing;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Capture;
using Clippings.Platform.Display;
using Clippings.Platform.Imaging;
using Clippings.Platform.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SkiaSharp;

namespace Clippings.App.Services;

public sealed record OverlayResult(SKBitmap Image, PixelRect Bounds, OverlayAction Action);

public enum CaptureInvocationSource
{
    UserInterface,
    GlobalHotkey,
    CommandLine,
}

public sealed class CaptureCoordinator
{
    private readonly ScreenCaptureService _capture;
    private readonly MonitorService _monitors;
    private readonly ForegroundWindowTracker _foregroundWindows;
    private readonly IClippingsStore _store;
    private readonly AppState _state;
    private readonly INotificationService _notifications;
    private readonly ILogger<CaptureCoordinator> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private bool _busy;

    public CaptureCoordinator(
        ScreenCaptureService capture,
        MonitorService monitors,
        ForegroundWindowTracker foregroundWindows,
        IClippingsStore store,
        AppState state,
        INotificationService notifications,
        ILogger<CaptureCoordinator> logger,
        ILoggerFactory loggerFactory)
    {
        _capture = capture;
        _monitors = monitors;
        _foregroundWindows = foregroundWindows;
        _store = store;
        _state = state;
        _notifications = notifications;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public Window? MainWindow { get; set; }

    public ScreenAnnotationService? AnnotationService { get; set; }

    public event EventHandler? CaptureStored;

    public async Task CaptureAsync(
        CaptureMode mode,
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        var mainWindow = MainWindow;
        var annotations = AnnotationService;
        var restoreMainWindow = mainWindow?.IsVisible == true;
        WindowCaptureTarget? windowTarget = null;
        SKBitmap? screen = null;
        try
        {
            if (mode == CaptureMode.Window)
            {
                if (!_foregroundWindows.TryGetTarget(out var target))
                {
                    throw new InvalidOperationException("找不到可擷取的目前視窗。");
                }

                windowTarget = target;
            }

            mainWindow?.Hide();
            annotations?.PrepareForCapture();
            await Task.Delay(180);

            if (mode == CaptureMode.Delayed)
            {
                await ShowCountdownAsync(Math.Max(1, _state.Settings.DelaySeconds));
                mode = CaptureMode.Region;
            }

            var monitors = _monitors.GetMonitors();
            if (monitors.Count == 0)
            {
                throw new InvalidOperationException("找不到可擷取的螢幕。");
            }

            if (mode == CaptureMode.Window)
            {
                if (windowTarget is not { } target ||
                    !WindowHelper.TryGetVisibleWindowBounds(target, out var windowBounds))
                {
                    throw new InvalidOperationException("目前視窗已關閉、最小化或無法擷取。");
                }

                var image = await Task.Run(() => _capture.CaptureScreenRegion(windowBounds, monitors));
                await HandleCaptureAsync(image, OverlayAction.Copy, CaptureMode.Window, source);
                restoreMainWindow = false;
                return;
            }

            var captured = await Task.Run(() => _capture.CaptureVirtualScreen(monitors));
            screen = captured.Bitmap;
            var virtualBounds = captured.Bounds;

            if (mode == CaptureMode.Region)
            {
                var overlayScreen = screen!;
                screen = null;
                annotations?.SuspendOverlays();
                OverlayResult? result;
                try
                {
                    result = await RunOverlayAsync(overlayScreen, virtualBounds, monitors);
                }
                finally
                {
                    annotations?.ResumeOverlays();
                }

                if (result is null)
                {
                    return;
                }

                await HandleCaptureAsync(result.Image, result.Action, CaptureMode.Region, source);
                restoreMainWindow = false;
            }
            else
            {
                SKBitmap image;
                if (mode == CaptureMode.FullScreen)
                {
                    image = screen!;
                    screen = null;
                }
                else
                {
                    var region = GetCurrentMonitorBounds(virtualBounds, monitors);
                    image = await Task.Run(() => ImageCodec.Crop(screen!, region));
                    screen.Dispose();
                    screen = null;
                }

                await HandleCaptureAsync(image, OverlayAction.Copy, mode, source);
                restoreMainWindow = false;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "擷取流程失敗。");
            MessageBox.Show(AppStrings.CaptureFailed + " " + exception.Message, AppStrings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            screen?.Dispose();
            _busy = false;
            annotations?.RestoreAfterCapture();
            if (restoreMainWindow && mainWindow is not null)
            {
                mainWindow.Show();
                mainWindow.Activate();
            }
        }
    }

    public Task CaptureRegionAsync(
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
        => CaptureAsync(CaptureMode.Region, source);

    public Task CaptureFullScreenAsync(
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
        => CaptureAsync(CaptureMode.FullScreen, source);

    public Task CaptureMonitorAsync(
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
        => CaptureAsync(CaptureMode.Monitor, source);

    public Task CaptureDelayedAsync(
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
        => CaptureAsync(CaptureMode.Delayed, source);

    public Task CaptureWindowAsync(
        CaptureInvocationSource source = CaptureInvocationSource.UserInterface)
        => CaptureAsync(CaptureMode.Window, source);

    private PixelRect GetCurrentMonitorBounds(PixelRect virtualBounds, IReadOnlyList<MonitorInfo> monitors)
    {
        var (cursorX, cursorY) = WindowHelper.GetCursorPosition();
        var monitor = monitors.FirstOrDefault(item =>
            cursorX >= item.Bounds.X && cursorX < item.Bounds.Right &&
            cursorY >= item.Bounds.Y && cursorY < item.Bounds.Bottom);
        if (monitor is null)
        {
            return virtualBounds;
        }

        return monitor.Bounds.Offset(-virtualBounds.X, -virtualBounds.Y);
    }

    private async Task<OverlayResult?> RunOverlayAsync(
        SKBitmap screen,
        PixelRect virtualBounds,
        IReadOnlyList<MonitorInfo> monitors)
    {
        var completion = new TaskCompletionSource<OverlayResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlays = new List<CaptureOverlayWindow>();
        var crops = new List<SKBitmap>();
        var bitmapSources = new List<BitmapSource>();
        var screenDisposed = false;
        var finished = false;

        void Finish(OverlayResult? result)
        {
            if (finished)
            {
                return;
            }

            finished = true;
            completion.TrySetResult(result);
        }

        void CloseAll()
        {
            foreach (var overlay in overlays)
            {
                if (overlay.IsVisible)
                {
                    overlay.Close();
                }
            }
        }

        try
        {
            await Task.Run(() =>
            {
                foreach (var monitor in monitors)
                {
                    var cropRect = monitor.Bounds.Offset(-virtualBounds.X, -virtualBounds.Y);
                    crops.Add(ImageCodec.Crop(screen, cropRect));
                }

                screen.Dispose();
                screenDisposed = true;

                foreach (var crop in crops)
                {
                    bitmapSources.Add(BitmapSourceHelper.FromSKBitmap(crop));
                }
            });

            for (var i = 0; i < monitors.Count; i++)
            {
                var overlay = new CaptureOverlayWindow(
                    monitors[i],
                    crops[i],
                    bitmapSources[i],
                    _state.Settings.ShowMagnifier);
                overlay.Completed += (_, args) =>
                {
                    Finish(new OverlayResult(args.Image, args.Bounds, args.Action));
                    CloseAll();
                };
                overlay.Cancelled += (_, _) =>
                {
                    Finish(null);
                    CloseAll();
                };
                overlays.Add(overlay);
            }

            foreach (var overlay in overlays)
            {
                overlay.Show();
            }

            var (cursorX, cursorY) = WindowHelper.GetCursorPosition();
            var active = overlays.FirstOrDefault(overlay =>
                cursorX >= overlay.Monitor.Bounds.X && cursorX < overlay.Monitor.Bounds.Right &&
                cursorY >= overlay.Monitor.Bounds.Y && cursorY < overlay.Monitor.Bounds.Bottom) ?? overlays[0];
            active.Activate();
            active.Focus();

            var result = await completion.Task;
            CloseAll();
            await Task.Yield();
            return result;
        }
        finally
        {
            if (!screenDisposed)
            {
                screen.Dispose();
            }

            foreach (var crop in crops)
            {
                crop.Dispose();
            }
        }
    }

    private async Task HandleCaptureAsync(
        SKBitmap image,
        OverlayAction action,
        CaptureMode mode,
        CaptureInvocationSource source)
    {
        try
        {
            _logger.LogInformation("擷取完成：{Action} {Width}x{Height}", action, image.Width, image.Height);
            var settings = _state.Settings;
            if (source == CaptureInvocationSource.GlobalHotkey && settings.PlayCaptureSound)
            {
                TryPlayCaptureSound();
            }

            var (png, thumbnailBytes, hash) = await Task.Run(() =>
            {
                var encoded = ImageCodec.Encode(image, ImageFormat.Png);
                var thumbnail = ImageCodec.EncodeThumbnailJpeg(image);
                return (encoded, thumbnail, HashUtil.Sha256Hex(encoded));
            });

            var item = new CaptureItem
            {
                CreatedAt = DateTimeOffset.Now,
                Width = image.Width,
                Height = image.Height,
                Source = ScreenCaptureService.DescribeCaptureMode(mode),
                Hash = hash,
            };

            try
            {
                await _store.AddCaptureAsync(item, png, "png", thumbnailBytes);
                await _store.TrimCapturesAsync(settings.MaxCaptureItems, settings.RetentionDays);
                CaptureStored?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "寫入 Clippings 歷史庫失敗。");
                _notifications.Show(AppStrings.CaptureHistorySaveFailed, isError: true);
            }

            if (action == OverlayAction.Copy || settings.AutoCopy)
            {
                if (!await CopyToClipboardAsync(image))
                {
                    _notifications.Show(AppStrings.ClipboardCopyFailed, isError: true);
                }
            }

            if (action == OverlayAction.Save)
            {
                await SaveAsAsync(image);
            }
            else if (settings.SaveAutomatically && action != OverlayAction.Edit)
            {
                await SaveToDirectoryAsync(image, png, item.CreatedAt);
            }

            if (action == OverlayAction.Edit || source == CaptureInvocationSource.GlobalHotkey)
            {
                OpenEditor(image, item);
            }
            else
            {
                image.Dispose();
            }
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static async Task<bool> CopyToClipboardAsync(SKBitmap image)
    {
        var source = await Task.Run(() => BitmapSourceHelper.FromSKBitmap(image));
        return ClipboardHelper.TrySetImage(source);
    }

    private async Task SaveToDirectoryAsync(SKBitmap image, byte[] historyPng, DateTimeOffset createdAt)
    {
        try
        {
            var settings = _state.Settings;
            var directory = string.IsNullOrWhiteSpace(settings.SaveDirectory)
                ? PathUtil.DefaultSaveRoot
                : settings.SaveDirectory;
            var format = settings.DefaultFormat;
            var extension = format == ImageFormat.Jpeg ? "jpg" : "png";
            var bytes = format == ImageFormat.Jpeg
                ? await Task.Run(() => ImageCodec.Encode(image, format, settings.JpegQuality))
                : historyPng;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, PathUtil.BuildImageFileName(createdAt, HashUtil.Sha256Hex(bytes), extension));
            await File.WriteAllBytesAsync(path, bytes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "自動儲存圖檔失敗。");
            _notifications.Show(AppStrings.AutoSaveFailed, isError: true);
        }
    }

    private async Task SaveAsAsync(SKBitmap image)
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
            InitialDirectory = string.IsNullOrWhiteSpace(_state.Settings.SaveDirectory)
                ? PathUtil.DefaultSaveRoot
                : _state.Settings.SaveDirectory,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var format = Path.GetExtension(dialog.FileName).ToLowerInvariant() is ".jpg" or ".jpeg"
                ? ImageFormat.Jpeg
                : ImageFormat.Png;
            var bytes = await Task.Run(() => ImageCodec.Encode(image, format, settings.JpegQuality));
            await File.WriteAllBytesAsync(dialog.FileName, bytes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "另存圖檔失敗。");
            MessageBox.Show("儲存失敗：" + exception.Message, AppStrings.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenEditor(SKBitmap image, CaptureItem origin)
    {
        AnnotationService?.Stop();
        var editor = new EditorWindow(image, _state, _store, _loggerFactory, origin, canDiscard: true);
        editor.Show();
        editor.Activate();
    }

    private void TryPlayCaptureSound()
    {
        try
        {
            SystemSounds.Asterisk.Play();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "播放擷取完成音效失敗。");
        }
    }


    private static async Task ShowCountdownAsync(int seconds)
    {
        var text = new System.Windows.Controls.TextBlock
        {
            Text = seconds.ToString(),
            FontSize = 72,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var caption = new System.Windows.Controls.TextBlock
        {
            Text = "秒後擷取",
            FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 22),
            VerticalAlignment = VerticalAlignment.Bottom,
        };

        var border = new System.Windows.Controls.Border
        {
            CornerRadius = new CornerRadius(24),
            Background = new SolidColorBrush(Color.FromArgb(0xD9, 0x1E, 0x24, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = new System.Windows.Controls.Grid
            {
                Children = { text, caption },
            },
        };

        var window = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            Topmost = true,
            ShowInTaskbar = false,
            ShowActivated = false,
            Focusable = false,
            IsHitTestVisible = false,
            Width = 220,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = border,
        };

        window.Show();
        try
        {
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                text.Text = remaining.ToString();
                await Task.Delay(1000);
            }
        }
        finally
        {
            window.Close();
        }
    }
}






