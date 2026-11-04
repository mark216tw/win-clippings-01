using System.Windows;
using Clippings.App.Views;
using Clippings.Platform.Interop;

namespace Clippings.App.Services;

public sealed class RecordingCountdownService
{
    public Task<bool> ShowAsync(
        RecordingTarget target,
        int seconds,
        Action<int> remainingChanged,
        CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            return ShowCoreAsync(target, seconds, remainingChanged, cancellationToken);
        }

        return dispatcher.InvokeAsync(
            () => ShowCoreAsync(target, seconds, remainingChanged, cancellationToken)).Task.Unwrap();
    }

    private static Task<bool> ShowCoreAsync(
        RecordingTarget target,
        int seconds,
        Action<int> remainingChanged,
        CancellationToken cancellationToken)
    {
        var window = new RecordingCountdownWindow(target, seconds, remainingChanged);
        return window.RunAsync(cancellationToken);
    }
}
