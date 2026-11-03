namespace Clippings.Platform.Interop;

public readonly record struct WindowCaptureTarget(IntPtr Handle, uint ProcessId);
