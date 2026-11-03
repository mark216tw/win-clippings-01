using Clippings.Core.Input;

namespace Clippings.Core.Tests;

public class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+X", HotkeyModifiers.Control | HotkeyModifiers.Shift, (uint)'X')]
    [InlineData("ctrl+alt+f5", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x74u)]
    [InlineData("Win+PrintScreen", HotkeyModifiers.Win, 0x2Cu)]
    public void TryParse_ValidGestures(string text, HotkeyModifiers modifiers, uint key)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var gesture));
        Assert.NotNull(gesture);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("X")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Shift+NotAKey")]
    [InlineData("Ctrl+X+Y")]
    public void TryParse_RejectsInvalidGestures(string text)
    {
        Assert.False(HotkeyGesture.TryParse(text, out _));
    }

    [Fact]
    public void DisplayName_RoundTrips()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Shift, 'X');

        Assert.Equal("Ctrl+Shift+X", gesture.DisplayName);
        Assert.True(HotkeyGesture.TryParse(gesture.DisplayName, out var parsed));
        Assert.Equal(gesture, parsed);
    }
}
