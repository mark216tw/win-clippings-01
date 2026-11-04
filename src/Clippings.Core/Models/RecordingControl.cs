using System.Diagnostics;

namespace Clippings.Core.Models;

public sealed class RecordingControl
{
    private readonly object _sync = new();
    private readonly Stopwatch _clock = new();
    private TaskCompletionSource? _pauseWaiter;
    private bool _started;
    private bool _paused;
    private long _pauseGeneration;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _paused;
            }
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_sync)
            {
                return _clock.Elapsed;
            }
        }
    }

    public long PauseGeneration
    {
        get
        {
            lock (_sync)
            {
                return _pauseGeneration;
            }
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            if (!_paused)
            {
                _clock.Start();
            }
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            if (_paused)
            {
                return;
            }

            _paused = true;
            _pauseGeneration++;
            if (_started)
            {
                _clock.Stop();
            }

            _pauseWaiter ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Resume()
    {
        TaskCompletionSource? waiter;
        lock (_sync)
        {
            if (!_paused)
            {
                return;
            }

            _paused = false;
            if (_started)
            {
                _clock.Start();
            }

            waiter = _pauseWaiter;
            _pauseWaiter = null;
        }

        waiter?.TrySetResult();
    }

    public Task WaitWhilePausedAsync()
    {
        lock (_sync)
        {
            if (!_paused)
            {
                return Task.CompletedTask;
            }

            _pauseWaiter ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _pauseWaiter.Task;
        }
    }

    public void ReleaseWaiters()
    {
        TaskCompletionSource? waiter;
        lock (_sync)
        {
            _paused = false;
            _clock.Stop();
            waiter = _pauseWaiter;
            _pauseWaiter = null;
        }

        waiter?.TrySetResult();
    }
}
