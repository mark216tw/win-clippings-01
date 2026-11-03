using System.Windows.Interop;
using Clippings.Core.Input;
using Clippings.Platform.Interop;

namespace Clippings.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyWindowId = 0x4A11;

    private readonly Dictionary<int, Action> _handlers = new();
    private HwndSource? _source;
    private int _nextId = HotkeyWindowId;
    private int _dispatchSuppressionCount;
    private bool _disposed;

    public IDisposable SuppressDispatch()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _dispatchSuppressionCount++;
        return new DispatchSuppression(this);
    }

    public void Initialize()
    {
        if (_source is not null)
        {
            return;
        }

        var parameters = new HwndSourceParameters("Clippings.HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = new IntPtr(-3),
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WindowProcedure);
    }

    public bool TryRegister(HotkeyGesture gesture, Action callback)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ArgumentNullException.ThrowIfNull(callback);

        Initialize();
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var id = _nextId++;
        var modifiers = (uint)gesture.Modifiers | NativeMethods.ModNoRepeat;
        if (!NativeMethods.RegisterHotKey(handle, id, modifiers, gesture.VirtualKey))
        {
            return false;
        }

        _handlers[id] = callback;
        return true;
    }

    public void UnregisterAll()
    {
        var handle = _source?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
        {
            foreach (var id in _handlers.Keys)
            {
                _ = NativeMethods.UnregisterHotKey(handle, id);
            }
        }

        _handlers.Clear();
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeMethods.WmHotkey && _handlers.TryGetValue(wParam.ToInt32(), out var callback))
        {
            handled = true;
            if (_dispatchSuppressionCount == 0)
            {
                callback();
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _source?.RemoveHook(WindowProcedure);
        _source?.Dispose();
        _source = null;
    }

    private void ResumeDispatch()
    {
        if (_dispatchSuppressionCount > 0)
        {
            _dispatchSuppressionCount--;
        }
    }

    private sealed class DispatchSuppression(GlobalHotkeyService owner) : IDisposable
    {
        private GlobalHotkeyService? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.ResumeDispatch();
        }
    }
}
