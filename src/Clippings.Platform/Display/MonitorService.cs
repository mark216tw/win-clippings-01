using System.Runtime.InteropServices;
using Clippings.Core.Editing;
using Clippings.Platform.Interop;

namespace Clippings.Platform.Display;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        var index = 0;

        NativeMethods.MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.Rect monitorRect, IntPtr data) =>
        {
            _ = hdcMonitor;
            _ = monitorRect;
            _ = data;

            var info = new NativeMethods.MonitorInfoEx
            {
                Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>(),
                DeviceName = string.Empty,
            };

            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var dpi = 96u;
            if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MdtEffectiveDpi, out var dpiX, out _) == 0)
            {
                dpi = dpiX;
            }

            var bounds = new PixelRect(
                info.Monitor.Left,
                info.Monitor.Top,
                info.Monitor.Right - info.Monitor.Left,
                info.Monitor.Bottom - info.Monitor.Top);
            var work = new PixelRect(
                info.Work.Left,
                info.Work.Top,
                info.Work.Right - info.Work.Left,
                info.Work.Bottom - info.Work.Top);

            monitors.Add(new MonitorInfo(
                index++,
                hMonitor,
                info.DeviceName,
                bounds,
                work,
                dpi == 0 ? 96u : dpi,
                (info.Flags & NativeMethods.MonitorInfoPrimary) != 0));

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        return monitors
            .OrderBy(monitor => monitor.Bounds.X)
            .ThenBy(monitor => monitor.Bounds.Y)
            .ToList();
    }

    public PixelRect GetVirtualBounds(IReadOnlyList<MonitorInfo> monitors)
    {
        if (monitors.Count == 0)
        {
            return PixelRect.Empty;
        }

        var left = monitors.Min(monitor => monitor.Bounds.X);
        var top = monitors.Min(monitor => monitor.Bounds.Y);
        var right = monitors.Max(monitor => monitor.Bounds.Right);
        var bottom = monitors.Max(monitor => monitor.Bounds.Bottom);
        return new PixelRect(left, top, right - left, bottom - top);
    }

    public MonitorInfo? GetMonitorAt(int x, int y)
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(monitor =>
            x >= monitor.Bounds.X && x < monitor.Bounds.Right &&
            y >= monitor.Bounds.Y && y < monitor.Bounds.Bottom);
    }

    public MonitorInfo? GetMonitorForRect(PixelRect rect)
        => GetMonitorAt(rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    public MonitorInfo? GetPrimaryMonitor()
        => GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary);
}
