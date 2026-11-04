using Clippings.Core.Models;

namespace Clippings.Core.Tests;

public sealed class RecordingControlTests
{
    [Fact]
    public void PauseAndResume_ReleasePausedWaiters()
    {
        var control = new RecordingControl();
        control.Start();

        Assert.True(control.WaitWhilePausedAsync().IsCompleted);

        control.Pause();
        Assert.True(control.IsPaused);

        var waiter = control.WaitWhilePausedAsync();
        Assert.False(waiter.IsCompleted);

        control.Resume();
        Assert.False(control.IsPaused);
        Assert.True(waiter.IsCompleted);
    }

    [Fact]
    public void ReleaseWaiters_CompletesPausedWaiter()
    {
        var control = new RecordingControl();
        control.Start();
        control.Pause();

        var waiter = control.WaitWhilePausedAsync();
        Assert.False(waiter.IsCompleted);

        control.ReleaseWaiters();

        Assert.False(control.IsPaused);
        Assert.True(waiter.IsCompleted);
    }

    [Fact]
    public void PauseBeforeStart_DoesNotAdvanceElapsedUntilResume()
    {
        var control = new RecordingControl();
        control.Pause();
        control.Start();

        var pausedElapsed = control.Elapsed;
        Thread.Sleep(30);
        Assert.Equal(pausedElapsed, control.Elapsed);

        control.Resume();
        Thread.Sleep(30);
        Assert.True(control.Elapsed > pausedElapsed);
    }

    [Fact]
    public void PauseFreezesElapsed()
    {
        var control = new RecordingControl();
        control.Start();
        Thread.Sleep(20);

        control.Pause();
        var pausedElapsed = control.Elapsed;
        Thread.Sleep(40);

        Assert.Equal(pausedElapsed, control.Elapsed);

        control.Resume();
        Thread.Sleep(20);
        Assert.True(control.Elapsed > pausedElapsed);
    }

    [Fact]
    public void PauseGeneration_IncrementsOnlyForNewPause()
    {
        var control = new RecordingControl();

        Assert.Equal(0, control.PauseGeneration);
        control.Pause();
        control.Pause();
        Assert.Equal(1, control.PauseGeneration);

        control.Resume();
        control.Pause();
        Assert.Equal(2, control.PauseGeneration);
    }
}
