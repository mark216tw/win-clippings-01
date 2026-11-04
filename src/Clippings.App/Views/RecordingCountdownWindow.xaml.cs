using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Clippings.Core.Editing;
using Clippings.Platform.Interop;

namespace Clippings.App.Views;

public partial class RecordingCountdownWindow : Window
{
    private readonly RecordingTarget _target;
    private readonly int _seconds;
    private readonly Action<int> _remainingChanged;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _cancellationRegistration;
    private bool _finished;
    private int _lastRemaining;

    public RecordingCountdownWindow(
        RecordingTarget target,
        int seconds,
        Action<int> remainingChanged)
    {
        _target = target;
        _seconds = seconds;
        _remainingChanged = remainingChanged;
        _lastRemaining = seconds;
        InitializeComponent();
        SecondsText.Text = seconds.ToString();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnTimerTick, Dispatcher);
    }

    public Task<bool> RunAsync(CancellationToken cancellationToken)
    {
        _cancellationRegistration = cancellationToken.Register(
            () => _ = Dispatcher.BeginInvoke(() => Finish(false)));
        _remainingChanged(_seconds);
        Show();
        _clock.Start();
        _timer.Start();
        return _completion.Task;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _ = WindowHelper.ExcludeFromCapture(handle);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
        => _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, PositionWindow);

    private void PositionWindow()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var scale = _target.Monitor.ScaleFactor;
        var width = checked((int)Math.Round(ActualWidth * scale));
        var height = checked((int)Math.Round(ActualHeight * scale));
        var bounds = _target.Bounds;
        var left = bounds.X + ((bounds.Width - width) / 2);
        var top = bounds.Y + ((bounds.Height - height) / 2);
        WindowHelper.SetPhysicalBounds(handle, new PixelRect(left, top, width, height));
        Opacity = 1;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var remaining = Math.Max(0, (int)Math.Ceiling(_seconds - _clock.Elapsed.TotalSeconds));
        if (remaining != _lastRemaining)
        {
            _lastRemaining = remaining;
            SecondsText.Text = remaining.ToString();
            _remainingChanged(remaining);
        }

        if (remaining == 0)
        {
            Finish(true);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Finish(false);
            e.Handled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Finish(false);

    private void OnClosed(object? sender, EventArgs e)
    {
        if (!_finished)
        {
            _finished = true;
            _timer.Stop();
            _clock.Stop();
            _cancellationRegistration.Dispose();
            _completion.TrySetResult(false);
        }
    }

    private void Finish(bool completed)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _timer.Stop();
        _clock.Stop();
        _cancellationRegistration.Dispose();
        _completion.TrySetResult(completed);
        Close();
    }
}
