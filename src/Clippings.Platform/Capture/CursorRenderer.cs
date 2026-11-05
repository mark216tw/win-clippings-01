using System.Runtime.InteropServices;
using Clippings.Core.Editing;
using Clippings.Platform.Interop;

namespace Clippings.Platform.Capture;

internal static class CursorRenderer
{
    public static void TryDraw(IntPtr hdc, PixelRect bounds)
    {
        if (hdc == IntPtr.Zero || bounds.IsEmpty)
        {
            return;
        }

        var cursor = new NativeMethods.CursorInfo
        {
            Size = Marshal.SizeOf<NativeMethods.CursorInfo>(),
        };

        if (!NativeMethods.GetCursorInfo(ref cursor) ||
            (cursor.Flags & NativeMethods.CursorShowing) == 0 ||
            cursor.Cursor == IntPtr.Zero)
        {
            return;
        }

        var screenX = cursor.ScreenPosition.X;
        var screenY = cursor.ScreenPosition.Y;
        if (screenX < bounds.X || screenX >= bounds.Right ||
            screenY < bounds.Y || screenY >= bounds.Bottom)
        {
            return;
        }

        var hotspotX = 0;
        var hotspotY = 0;
        if (NativeMethods.GetIconInfo(cursor.Cursor, out var icon))
        {
            hotspotX = icon.HotspotX;
            hotspotY = icon.HotspotY;

            if (icon.MaskBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.MaskBitmap);
            }

            if (icon.ColorBitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(icon.ColorBitmap);
            }
        }

        var x = screenX - hotspotX - bounds.X;
        var y = screenY - hotspotY - bounds.Y;
        _ = NativeMethods.DrawIconEx(
            hdc,
            x,
            y,
            cursor.Cursor,
            0,
            0,
            0,
            IntPtr.Zero,
            NativeMethods.DiNormal);
    }
}
