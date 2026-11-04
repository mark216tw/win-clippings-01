namespace Clippings.Platform.Interop;

public sealed record WindowRecordingTarget(
    WindowCaptureTarget Target,
    string Title,
    string ProcessName)
{
    public string DisplayName => $"{Title}  -  {ProcessName}";
}
