namespace Clippings.Core.Input;

public static class RecordingHotkeys
{
    public static HotkeyGesture PauseResume { get; } = new(
        HotkeyModifiers.Control | HotkeyModifiers.Shift,
        (uint)'P');

    public static HotkeyGesture Stop { get; } = new(
        HotkeyModifiers.Control | HotkeyModifiers.Shift,
        (uint)'S');
}
