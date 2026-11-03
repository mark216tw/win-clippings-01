using Clippings.Core.Editing;
using Clippings.Core.Models;
using SkiaSharp;

namespace Clippings.Platform.Imaging;

public static class ImageCodec
{
    public static byte[] Encode(SKBitmap bitmap, ImageFormat format, int quality = 90)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            format == ImageFormat.Png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
            Math.Clamp(quality, 1, 100));

        return data.ToArray();
    }

    public static SKBitmap Decode(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        var bitmap = SKBitmap.Decode(data);
        return bitmap ?? throw new InvalidOperationException("無法解碼影像資料。");
    }

    public static SKBitmap? TryDecode(byte[] bytes)
    {
        try
        {
            return Decode(bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static SKBitmap Load(string path) => Decode(File.ReadAllBytes(path));

    public static SKBitmap Crop(SKBitmap source, PixelRect rect)
    {
        var clamped = rect.ClampTo(source.Width, source.Height);
        if (clamped.IsEmpty)
        {
            throw new ArgumentException("裁切範圍無效。", nameof(rect));
        }

        var result = new SKBitmap(new SKImageInfo(clamped.Width, clamped.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(
            image,
            SKRect.Create(clamped.X, clamped.Y, clamped.Width, clamped.Height),
            SKRect.Create(0, 0, clamped.Width, clamped.Height),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        canvas.Flush();
        return result;
    }

    public static SKBitmap RotateFlip(SKBitmap source, RotateFlipKind kind)
    {
        ArgumentNullException.ThrowIfNull(source);

        var swap = kind is RotateFlipKind.Rotate90Cw or RotateFlipKind.Rotate90Ccw;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;

        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        switch (kind)
        {
            case RotateFlipKind.Rotate90Cw:
                canvas.Translate(width, 0);
                canvas.RotateDegrees(90);
                break;
            case RotateFlipKind.Rotate90Ccw:
                canvas.Translate(0, height);
                canvas.RotateDegrees(-90);
                break;
            case RotateFlipKind.FlipHorizontal:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case RotateFlipKind.FlipVertical:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "不支援的旋轉／翻轉方式。");
        }

        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(
            image,
            SKRect.Create(0, 0, source.Width, source.Height),
            SKRect.Create(0, 0, source.Width, source.Height),
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        canvas.Flush();
        return result;
    }

    public static byte[] EncodeThumbnailJpeg(SKBitmap source, int maxSize = 320, int quality = 82)
    {
        using var thumbnail = CreateThumbnail(source, maxSize);
        using var flattened = new SKBitmap(
            new SKImageInfo(thumbnail.Width, thumbnail.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(flattened))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(
                thumbnail,
                SKRect.Create(0, 0, thumbnail.Width, thumbnail.Height),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None),
                null);
            canvas.Flush();
        }

        return Encode(flattened, ImageFormat.Jpeg, quality);
    }

    public static SKBitmap CreateThumbnail(SKBitmap source, int maxSize = 320)
    {
        if (source.Width <= 0 || source.Height <= 0)
        {
            throw new ArgumentException("影像尺寸無效。", nameof(source));
        }

        var scale = Math.Min(maxSize / (double)source.Width, maxSize / (double)source.Height);
        scale = Math.Min(1.0, scale);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));

        var result = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(
            image,
            SKRect.Create(0, 0, width, height),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.Flush();
        return result;
    }
}
