namespace Clippings.Platform.Interop;

public sealed class ForegroundWindowTracker : IDisposable
{
    private readonly object _sync = new();
    private readonly NativeMethods.WinEventProc _callback;
    private IntPtr _hook;
    private WindowCaptureTarget _lastTarget;
    private bool _hasLastTarget;

    public ForegroundWindowTracker()
    {
        _callback = OnForegroundChanged;
        RememberCurrentForeground();
        _hook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);
    }

    public bool TryGetTarget(out WindowCaptureTarget target)
    {
        if (WindowHelper.TryGetForegroundCaptureTarget(out target))
        {
            Remember(target);
            return true;
        }

        lock (_sync)
        {
            if (!_hasLastTarget || !WindowHelper.TryGetVisibleWindowBounds(_lastTarget, out _))
            {
                target = default;
                _hasLastTarget = false;
                return false;
            }

            target = _lastTarget;
            return true;
        }
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void RememberCurrentForeground()
    {
        if (WindowHelper.TryGetForegroundCaptureTarget(out var target))
        {
            Remember(target);
        }
    }

    private void OnForegroundChanged(
        IntPtr hook,
        uint eventType,
        IntPtr handle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (WindowHelper.TryCreateCaptureTarget(handle, out var target))
        {
            Remember(target);
        }
    }

    private void Remember(WindowCaptureTarget target)
    {
        lock (_sync)
        {
            _lastTarget = target;
            _hasLastTarget = true;
        }
    }
}
