namespace Clippings.Platform.Interop;

public sealed class MiddleClickHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _callback;
    private readonly ManualResetEventSlim _started = new(false);
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _handle;
    private bool _disposed;

    public MiddleClickHook() => _callback = OnMouseMessage;

    public event EventHandler? MiddleClick;

    public Action<Exception>? ErrorHandler { get; set; }

    public bool IsInstalled => _handle != IntPtr.Zero;

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_thread is not null)
        {
            return IsInstalled;
        }

        _thread = new Thread(HookThreadMain)
        {
            IsBackground = true,
            Name = "Clippings.MiddleClickHook",
        };
        _thread.Start();

        return _started.Wait(TimeSpan.FromSeconds(2)) && IsInstalled;
    }

    private void HookThreadMain()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _handle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
        _started.Set();

        if (_handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                _ = NativeMethods.TranslateMessage(ref message);
                _ = NativeMethods.DispatchMessage(ref message);
            }
        }
        catch (Exception exception)
        {
            ErrorHandler?.Invoke(exception);
        }
    }

    private IntPtr OnMouseMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
        }

        if (wParam == (IntPtr)NativeMethods.WmMButtonDown)
        {
            RaiseMiddleClick();
            return new IntPtr(1);
        }

        if (wParam == (IntPtr)NativeMethods.WmMButtonUp)
        {
            return new IntPtr(1);
        }

        return NativeMethods.CallNextHookEx(_handle, code, wParam, lParam);
    }

    private void RaiseMiddleClick()
    {
        try
        {
            MiddleClick?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception exception)
        {
            ErrorHandler?.Invoke(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var thread = _thread;
        if (thread is not null && thread.IsAlive && _threadId != 0)
        {
            _ = NativeMethods.PostThreadMessage(
                _threadId,
                NativeMethods.WmQuit,
                IntPtr.Zero,
                IntPtr.Zero);
            _ = thread.Join(TimeSpan.FromSeconds(1));
        }

        if (_handle != IntPtr.Zero)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_handle);
            _handle = IntPtr.Zero;
        }

        _started.Dispose();
        GC.SuppressFinalize(this);
    }
}
