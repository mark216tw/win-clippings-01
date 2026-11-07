using System.Runtime.InteropServices;
using System.Text;
using Clippings.Core.Editing;

namespace Clippings.Platform.Interop;

public static class WindowHelper
{
    public static bool ExcludeFromCapture(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaExcludeFromCapture))
        {
            return true;
        }

        return NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaMonitor);
    }

    public static void SetClickThrough(IntPtr handle, bool clickThrough)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style = clickThrough
            ? style | NativeMethods.WsExTransparent
            : style & ~NativeMethods.WsExTransparent;
        _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }

    public static void SetOverlayWindowStyles(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
        style |= NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow;
        _ = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style));
    }

    public static void BringToTopMost(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopmost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    public static void ApplyDarkTitleBar(IntPtr handle, bool useDark)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var value = useDark ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    public static void ApplyRoundedCorners(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var preference = 2;
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaWindowCornerPreference, ref preference, sizeof(int));
    }

    public static bool TryApplyMica(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var backdrop = 2;
        return NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DwmwaSystemBackdropType, ref backdrop, sizeof(int)) == 0;
    }

    public static double GetWindowScale(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return 1.0;
        }

        var dpi = NativeMethods.GetDpiForWindow(handle);
        return dpi == 0 ? 1.0 : dpi / 96.0;
    }

    public static void SetPhysicalBounds(IntPtr handle, PixelRect bounds)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        _ = NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    public static (int X, int Y) GetCursorPosition()
        => NativeMethods.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);

    public static bool TryGetMonitorDpi(IntPtr monitorHandle, out uint dpi)
    {
        dpi = 96;
        return NativeMethods.GetDpiForMonitor(monitorHandle, NativeMethods.MdtEffectiveDpi, out dpi, out _) == 0;
    }

    public static bool TryGetForegroundCaptureTarget(out WindowCaptureTarget target)
        => TryCreateCaptureTarget(NativeMethods.GetForegroundWindow(), out target);

    public static bool TryFindCapturableWindow(out WindowCaptureTarget target)
    {
        var result = default(WindowCaptureTarget);
        var found = false;

        _ = NativeMethods.EnumWindows((handle, _data) =>
        {
            if (!TryCreateCaptureTarget(handle, out var candidate))
            {
                return true;
            }

            if (!TryGetVisibleWindowBounds(candidate, out var bounds) ||
                bounds.Width < 64 ||
                bounds.Height < 64 ||
                NativeMethods.GetWindowTextLength(candidate.Handle) <= 0)
            {
                return true;
            }

            var exStyle = NativeMethods.GetWindowLongPtr(candidate.Handle, NativeMethods.GwlExStyle).ToInt64();
            if ((exStyle & NativeMethods.WsExToolWindow) != 0)
            {
                return true;
            }

            result = candidate;
            found = true;
            return false;
        }, IntPtr.Zero);

        target = result;
        return found;
    }

    public static bool TryActivateWindow(WindowCaptureTarget target, bool restoreIfMinimized = false)
    {
        if (target.Handle == IntPtr.Zero || !NativeMethods.IsWindow(target.Handle))
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(target.Handle, out var processId);
        if (processId == 0 || processId != target.ProcessId)
        {
            return false;
        }

        if (restoreIfMinimized && NativeMethods.IsIconic(target.Handle))
        {
            _ = NativeMethods.ShowWindowAsync(target.Handle, NativeMethods.SwRestore);
        }

        return NativeMethods.SetForegroundWindow(target.Handle);
    }

    public static bool TryCreateCaptureTarget(IntPtr handle, out WindowCaptureTarget target)
    {
        target = default;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var root = NativeMethods.GetAncestor(handle, NativeMethods.GaRoot);
        if (root != IntPtr.Zero)
        {
            handle = root;
        }

        _ = NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0 || processId == (uint)Environment.ProcessId || !IsCapturableWindow(handle))
        {
            return false;
        }

        target = new WindowCaptureTarget(handle, processId);
        return true;
    }

    public static bool TryGetVisibleWindowBounds(WindowCaptureTarget target, out PixelRect bounds)
    {
        bounds = PixelRect.Empty;
        if (!NativeMethods.IsWindow(target.Handle))
        {
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(target.Handle, out var processId);
        if (processId != target.ProcessId || !IsCapturableWindow(target.Handle))
        {
            return false;
        }

        NativeMethods.Rect rect;
        var result = NativeMethods.DwmGetWindowAttribute(
            target.Handle,
            NativeMethods.DwmwaExtendedFrameBounds,
            out rect,
            Marshal.SizeOf<NativeMethods.Rect>());
        if (result != 0 && !NativeMethods.GetWindowRect(target.Handle, out rect))
        {
            return false;
        }

        bounds = PixelRect.FromBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return !bounds.IsEmpty;
    }

    private static bool IsCapturableWindow(IntPtr handle)
    {
        if (!NativeMethods.IsWindow(handle) || !NativeMethods.IsWindowVisible(handle) || NativeMethods.IsIconic(handle))
        {
            return false;
        }

        if (NativeMethods.DwmGetWindowAttribute(
                handle,
                NativeMethods.DwmwaCloaked,
                out int cloaked,
                sizeof(int)) == 0 && cloaked != 0)
        {
            return false;
        }

        var className = new StringBuilder(64);
        _ = NativeMethods.GetClassName(handle, className, className.Capacity);
        return className.ToString() is not (
            "Shell_TrayWnd" or
            "Shell_SecondaryTrayWnd" or
            "Progman" or
            "WorkerW" or
            "TopLevelWindowForOverflowXamlIsland" or
            "NotifyIconOverflowWindow" or
            "XamlExplorerHostIslandWindow" or
            "Xaml_WindowedPopupClass" or
            "ApplicationManager_DesktopShellWindow" or
            "SystemTray_Main" or
            "TaskSwitcherWnd" or
            "MultitaskingViewFrame" or
            "ForegroundStaging");
    }
}
