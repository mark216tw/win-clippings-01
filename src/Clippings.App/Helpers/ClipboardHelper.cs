using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Clippings.App.Helpers;

public static class ClipboardHelper
{
    private const int RetryCount = 5;

    public static bool TrySetImage(BitmapSource source)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                Clipboard.SetImage(source);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }

    public static BitmapSource? TryGetImage()
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                return Clipboard.ContainsImage() ? Clipboard.GetImage() : null;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return null;
    }
}
