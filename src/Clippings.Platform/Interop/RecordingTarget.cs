using Clippings.Core.Editing;
using Clippings.Platform.Display;

namespace Clippings.Platform.Interop;

public enum RecordingTargetKind
{
    Window = 0,
    Monitor = 1,
}

public sealed record RecordingTarget(
    RecordingTargetKind Kind,
    IntPtr Handle,
    uint ProcessId,
    string DisplayName,
    PixelRect Bounds,
    MonitorInfo Monitor)
{
    public bool IsWindow => Kind == RecordingTargetKind.Window;
}
