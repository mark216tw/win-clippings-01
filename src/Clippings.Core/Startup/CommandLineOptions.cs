namespace Clippings.Core.Startup;

public sealed record CommandLineOptions
{
    public StartupCommand Command { get; init; }

    public bool AutoStart { get; init; }

    public bool HideMainWindow => Command switch
    {
        StartupCommand.CaptureRegion or
        StartupCommand.CaptureFullScreen or
        StartupCommand.CaptureMonitor or
        StartupCommand.CaptureWindow or
        StartupCommand.Annotate => true,
        StartupCommand.None => AutoStart,
        _ => false,
    };

    public static bool TryParse(
        IReadOnlyList<string>? args,
        out CommandLineOptions options,
        out string? invalidArgument)
    {
        options = new CommandLineOptions();
        invalidArgument = null;

        if (args is null || args.Count == 0)
        {
            return true;
        }

        if (args.Any(IsHelpArgument))
        {
            options = new CommandLineOptions { Command = StartupCommand.Help };
            return true;
        }

        var autoStart = false;
        var command = StartupCommand.None;

        foreach (var rawArgument in args)
        {
            var argument = rawArgument?.Trim() ?? string.Empty;
            if (argument.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
            {
                autoStart = true;
                continue;
            }

            var parsed = ParseCommand(argument);
            if (parsed == StartupCommand.None)
            {
                invalidArgument = argument;
                return false;
            }

            if (command != StartupCommand.None)
            {
                invalidArgument = argument;
                return false;
            }

            command = parsed;
        }

        options = new CommandLineOptions { Command = command, AutoStart = autoStart };
        return true;
    }

    private static bool IsHelpArgument(string argument)
        => argument.Trim().Equals("--help", StringComparison.OrdinalIgnoreCase);

    private static StartupCommand ParseCommand(string argument) => argument.ToLowerInvariant() switch
    {
        "--show" => StartupCommand.Show,
        "--capture-region" => StartupCommand.CaptureRegion,
        "--capture-full" => StartupCommand.CaptureFullScreen,
        "--capture-monitor" => StartupCommand.CaptureMonitor,
        "--capture-window" => StartupCommand.CaptureWindow,
        "--annotate" => StartupCommand.Annotate,
        _ => StartupCommand.None,
    };
}
