using System.Runtime.InteropServices;
using Clippings.Core.Editing;
using Clippings.Core.Models;
using Clippings.Platform.Interop;
using Clippings.Platform.Display;
using SkiaSharp;

namespace Clippings.Platform.Capture;

public sealed class ScreenCaptureService
{
    private readonly MonitorService _monitorService;

    public ScreenCaptureService(MonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public IReadOnlyList<MonitorInfo> GetMonitors() => _monitorService.GetMonitors();

    public (SKBitmap Bitmap, PixelRect Bounds) CaptureVirtualScreen(bool includeCursor = false)
        => CaptureVirtualScreen(_monitorService.GetMonitors(), includeCursor);

    public (SKBitmap Bitmap, PixelRect Bounds) CaptureVirtualScreen(IReadOnlyList<MonitorInfo> monitors, bool includeCursor = false)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        var bounds = _monitorService.GetVirtualBounds(monitors);
        if (bounds.IsEmpty)
        {
            throw new InvalidOperationException("找不到可擷取的螢幕。");
        }

        var bitmap = CaptureRegion(bounds, includeCursor);
        return (bitmap, bounds);
    }

    public SKBitmap CaptureMonitor(MonitorInfo monitor, bool includeCursor = false)
        => CaptureRegion(monitor.Bounds, includeCursor);

    public SKBitmap CaptureScreenRegion(PixelRect requestedBounds, IReadOnlyList<MonitorInfo> monitors, bool includeCursor = false)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        var virtualBounds = _monitorService.GetVirtualBounds(monitors);
        var visibleBounds = requestedBounds.Intersect(virtualBounds);
        if (visibleBounds.IsEmpty)
        {
            throw new InvalidOperationException("目前視窗不在可擷取的螢幕範圍內。");
        }

        return CaptureRegion(visibleBounds, includeCursor);
    }

    private static SKBitmap CaptureRegion(PixelRect bounds, bool includeCursor)
    {
        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            throw new InvalidOperationException("無法取得螢幕裝置內容。");
        }

        var memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        if (memoryDc == IntPtr.Zero)
        {
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            throw new InvalidOperationException("無法建立相容裝置內容。");
        }

        var bitmapInfo = new NativeMethods.BitmapInfo
        {
            Header = new NativeMethods.BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.BitmapInfoHeader>(),
                Width = bounds.Width,
                Height = -bounds.Height,
                Planes = 1,
                BitCount = 32,
                Compression = NativeMethods.BiRgb,
                SizeImage = (uint)(bounds.Width * bounds.Height * 4),
            },
        };

        var dibSection = NativeMethods.CreateDIBSection(
            screenDc,
            ref bitmapInfo,
            NativeMethods.DibRgbColors,
            out var bits,
            IntPtr.Zero,
            0);

        if (dibSection == IntPtr.Zero || bits == IntPtr.Zero)
        {
            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            throw new InvalidOperationException("無法建立點陣圖區段。");
        }

        var previous = NativeMethods.SelectObject(memoryDc, dibSection);
        var dibOwnedByBitmap = false;
        try
        {
            var copied = NativeMethods.BitBlt(
                memoryDc,
                0,
                0,
                bounds.Width,
                bounds.Height,
                screenDc,
                bounds.X,
                bounds.Y,
                NativeMethods.SrcCopy | NativeMethods.CaptureBlt);

            if (!copied)
            {
                throw new InvalidOperationException("螢幕擷取失敗。");
            }

            if (includeCursor)
            {
                CursorRenderer.TryDraw(memoryDc, bounds);
            }

            var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            var bitmap = new SKBitmap();
            if (!bitmap.InstallPixels(
                    info,
                    bits,
                    info.RowBytes,
                    (_, _) => NativeMethods.DeleteObject(dibSection),
                    null))
            {
                bitmap.Dispose();
                throw new InvalidOperationException("無法建立影像像素參考。");
            }

            dibOwnedByBitmap = true;

            var pixels = bitmap.GetPixelSpan();
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xFF;
            }

            return bitmap;
        }
        finally
        {
            if (previous != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, previous);
            }

            if (!dibOwnedByBitmap)
            {
                NativeMethods.DeleteObject(dibSection);
            }

            NativeMethods.DeleteDC(memoryDc);
            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    public static string DescribeCaptureMode(CaptureMode mode) => mode switch
    {
        CaptureMode.Region => "區域擷取",
        CaptureMode.FullScreen => "全螢幕擷取",
        CaptureMode.Monitor => "螢幕擷取",
        CaptureMode.Delayed => "延遲擷取",
        CaptureMode.Window => "視窗擷取",
        _ => "擷取",
    };
}
