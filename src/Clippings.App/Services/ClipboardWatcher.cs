using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clippings.App.Helpers;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Platform.Imaging;
using Clippings.Platform.Interop;
using Microsoft.Extensions.Logging;

namespace Clippings.App.Services;

public sealed record ClipboardSnapshot(
    byte[]? ImageBytes,
    byte[]? ThumbnailBytes,
    string Hash);

public sealed class ClipboardSnapshotEventArgs : EventArgs
{
    public ClipboardSnapshotEventArgs(ClipboardSnapshot snapshot)
    {
        Snapshot = snapshot;
    }

    public ClipboardSnapshot Snapshot { get; }
}

public sealed class ClipboardWatcher : IDisposable
{
    private readonly AppState _state;
    private readonly ILogger<ClipboardWatcher> _logger;
    private readonly DispatcherTimer _debounce;
    private readonly object _hashGate = new();
    private HwndSource? _source;
    private bool _listening;
    private string? _lastHash;
    private bool _disposed;

    public ClipboardWatcher(AppState state, ILogger<ClipboardWatcher> logger)
    {
        _state = state;
        _logger = logger;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _debounce.Tick += OnDebounceTick;
    }

    public event EventHandler<ClipboardSnapshotEventArgs>? Captured;

    public void Start()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("Clippings.ClipboardWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = new IntPtr(-3),
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WindowProcedure);
        SetEnabled(_state.Settings.ClipboardHistoryEnabled);
    }

    public void SetEnabled(bool enabled)
    {
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (enabled && !_listening)
        {
            _listening = NativeMethods.AddClipboardFormatListener(handle);
        }
        else if (!enabled && _listening)
        {
            _ = NativeMethods.RemoveClipboardFormatListener(handle);
            _listening = false;
        }
    }

    public void ResetLastHash()
    {
        lock (_hashGate)
        {
            _lastHash = null;
        }
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmClipboardUpdate)
        {
            _debounce.Stop();
            _debounce.Start();
        }

        return IntPtr.Zero;
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (!_state.Settings.ClipboardHistoryEnabled)
        {
            return;
        }

        var image = ClipboardHelper.TryGetImage();
        if (image is null)
        {
            return;
        }

        if (image.CanFreeze)
        {
            image.Freeze();
        }

        _ = Task.Run(() => EncodeImage(image));
    }

    private void EncodeImage(BitmapSource image)
    {
        try
        {
            using var bitmap = BitmapSourceHelper.ToSKBitmap(image);
            var hash = HashUtil.Sha256Hex(bitmap.GetPixelSpan());
            lock (_hashGate)
            {
                if (string.Equals(_lastHash, hash, StringComparison.Ordinal))
                {
                    return;
                }
            }

            var png = ImageCodec.Encode(bitmap, ImageFormat.Png);
            var thumbnail = ImageCodec.EncodeThumbnailJpeg(bitmap);
            lock (_hashGate)
            {
                _lastHash = hash;
            }

            Emit(new ClipboardSnapshot(png, thumbnail, hash));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "剪貼簿影像處理失敗。");
        }
    }

    private void Emit(ClipboardSnapshot snapshot)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        _ = dispatcher.InvokeAsync(() => Captured?.Invoke(this, new ClipboardSnapshotEventArgs(snapshot)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnDebounceTick;

        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero && _listening)
        {
            _ = NativeMethods.RemoveClipboardFormatListener(handle);
        }

        _source?.RemoveHook(WindowProcedure);
        _source?.Dispose();
        _source = null;
    }
}
