using Clippings.Core.Editing;

namespace Clippings.Platform.Display;

public sealed record MonitorInfo(
    int Index,
    IntPtr Handle,
    string DeviceName,
    PixelRect Bounds,
    PixelRect WorkArea,
    uint Dpi,
    bool IsPrimary)
{
    public double ScaleFactor => Dpi / 96.0;
}
