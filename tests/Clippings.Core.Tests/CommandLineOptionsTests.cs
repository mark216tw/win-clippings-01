using Clippings.Core.Startup;

namespace Clippings.Core.Tests;

public class CommandLineOptionsTests
{
    [Fact]
    public void TryParse_NoArguments_ReturnsNone()
    {
        Assert.True(CommandLineOptions.TryParse([], out var options, out var invalidArgument));
        Assert.Equal(StartupCommand.None, options.Command);
        Assert.False(options.AutoStart);
        Assert.False(options.HideMainWindow);
        Assert.Null(invalidArgument);
    }

    [Theory]
    [InlineData("--show", StartupCommand.Show)]
    [InlineData("--SHOW", StartupCommand.Show)]
    [InlineData("--capture-region", StartupCommand.CaptureRegion)]
    [InlineData("--capture-full", StartupCommand.CaptureFullScreen)]
    [InlineData("--capture-monitor", StartupCommand.CaptureMonitor)]
    [InlineData("--capture-window", StartupCommand.CaptureWindow)]
    [InlineData("--annotate", StartupCommand.Annotate)]
    public void TryParse_RecognizesCommands(string argument, StartupCommand expected)
    {
        Assert.True(CommandLineOptions.TryParse([argument], out var options, out _));
        Assert.Equal(expected, options.Command);
    }

    [Theory]
    [InlineData("--capture-region")]
    [InlineData("--capture-full")]
    [InlineData("--capture-monitor")]
    [InlineData("--capture-window")]
    [InlineData("--annotate")]
    public void TryParse_CaptureCommands_HideMainWindow(string argument)
    {
        Assert.True(CommandLineOptions.TryParse([argument], out var options, out _));
        Assert.True(options.HideMainWindow);
    }

    [Fact]
    public void TryParse_AutoStartWithoutCommand_HidesMainWindow()
    {
        Assert.True(CommandLineOptions.TryParse(["--autostart"], out var options, out _));
        Assert.Equal(StartupCommand.None, options.Command);
        Assert.True(options.AutoStart);
        Assert.True(options.HideMainWindow);
    }

    [Fact]
    public void TryParse_AutoStartWithCapture_HidesMainWindow()
    {
        Assert.True(CommandLineOptions.TryParse(["--autostart", "--capture-full"], out var options, out _));
        Assert.Equal(StartupCommand.CaptureFullScreen, options.Command);
        Assert.True(options.HideMainWindow);
    }

    [Fact]
    public void TryParse_ShowWithAutoStart_ShowsMainWindow()
    {
        Assert.True(CommandLineOptions.TryParse(["--autostart", "--show"], out var options, out _));
        Assert.Equal(StartupCommand.Show, options.Command);
        Assert.True(options.AutoStart);
        Assert.False(options.HideMainWindow);
    }

    [Fact]
    public void TryParse_HelpTakesPrecedence()
    {
        Assert.True(CommandLineOptions.TryParse(["--unknown", "--help"], out var options, out var invalidArgument));
        Assert.Equal(StartupCommand.Help, options.Command);
        Assert.Null(invalidArgument);
    }

    [Fact]
    public void TryParse_UnknownArgument_Fails()
    {
        Assert.False(CommandLineOptions.TryParse(["--capture-all"], out _, out var invalidArgument));
        Assert.Equal("--capture-all", invalidArgument);
    }

    [Fact]
    public void TryParse_MultipleCommands_Fails()
    {
        Assert.False(CommandLineOptions.TryParse(
            ["--capture-full", "--capture-region"],
            out _,
            out var invalidArgument));
        Assert.Equal("--capture-region", invalidArgument);
    }

    [Fact]
    public void TryParse_TrimsWhitespace()
    {
        Assert.True(CommandLineOptions.TryParse([" --capture-full "], out var options, out _));
        Assert.Equal(StartupCommand.CaptureFullScreen, options.Command);
    }
}
