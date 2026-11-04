using System.Windows;
using Clippings.App.Views;
using Clippings.Platform.Interop;

namespace Clippings.App.Services;

public sealed class RecordingOverlayService
{
    private RecordingOverlayWindow? _window;

    public void Show(
        RecordingTarget target,
        Func<TimeSpan> elapsedProvider,
        Func<Task> pauseAsync,
        Func<Task> resumeAsync,
        Func<Task> stopAsync)
    {
        Dispatch(() =>
        {
            _window?.Close();
            _window = new RecordingOverlayWindow(target, elapsedProvider, pauseAsync, resumeAsync, stopAsync);
            _window.Show();
        });
    }

    public void SetPaused(bool paused)
        => Dispatch(() => _window?.SetPaused(paused));

    public void Hide()
    {
        Dispatch(() =>
        {
            _window?.Close();
            _window = null;
        });
    }

    private static void Dispatch(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _ = dispatcher.BeginInvoke(action);
        }
    }
}
