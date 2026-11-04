using System.Windows;
using System.Windows.Media.Imaging;

namespace Clippings.App.Helpers;

public sealed class PathToImageConverter : System.Windows.Data.IValueConverter
{
    private const int MaxCacheEntries = 2048;

    private static readonly Dictionary<(string Path, int Width), CacheEntry> Cache = [];
    private static readonly object CacheGate = new();

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is not string path)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return null;
            }

            var decodeWidth = parameter is string text && int.TryParse(text, out var parsed) && parsed > 0
                ? parsed
                : 400;
            var key = (path, decodeWidth);
            var stamp = file.LastWriteTimeUtc.Ticks;

            lock (CacheGate)
            {
                if (Cache.TryGetValue(key, out var cached) &&
                    cached.Stamp == stamp &&
                    cached.TryGetImage(out var cachedImage) &&
                    cachedImage is not null)
                {
                    return cachedImage;
                }
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            lock (CacheGate)
            {
                if (Cache.Count >= MaxCacheEntries)
                {
                    Cache.Clear();
                }

                Cache[key] = new CacheEntry(stamp, image);
            }

            return image;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or UriFormatException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private sealed class CacheEntry(long stamp, BitmapImage image)
    {
        private readonly WeakReference<BitmapImage> _image = new(image);

        public long Stamp { get; } = stamp;

        public bool TryGetImage(out BitmapImage? image) => _image.TryGetTarget(out image);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
