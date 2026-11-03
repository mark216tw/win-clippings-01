using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Clippings.App.Helpers;

public static class BitmapSourceHelper
{
    public static BitmapSource FromSKBitmap(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var info = bitmap.Info;
        var stride = info.RowBytes;
        var pixels = bitmap.GetPixels();
        if (pixels == IntPtr.Zero)
        {
            throw new InvalidOperationException("影像沒有可用的像素資料。");
        }

        var format = bitmap.AlphaType switch
        {
            SKAlphaType.Opaque => System.Windows.Media.PixelFormats.Bgr32,
            SKAlphaType.Premul => System.Windows.Media.PixelFormats.Pbgra32,
            _ => System.Windows.Media.PixelFormats.Bgra32,
        };

        var source = BitmapSource.Create(
            info.Width,
            info.Height,
            96,
            96,
            format,
            null,
            pixels,
            stride * info.Height,
            stride);
        source.Freeze();
        return source;
    }

    public static SKBitmap ToSKBitmap(BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        BitmapSource converted = source;
        if (source.Format != System.Windows.Media.PixelFormats.Bgra32)
        {
            converted = new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
        }

        var stride = converted.PixelWidth * 4;
        var bitmap = new SKBitmap(
            new SKImageInfo(converted.PixelWidth, converted.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        converted.CopyPixels(
            new System.Windows.Int32Rect(0, 0, converted.PixelWidth, converted.PixelHeight),
            bitmap.GetPixels(),
            stride * converted.PixelHeight,
            stride);
        return bitmap;
    }
}
