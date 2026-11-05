using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using WinRT;

namespace Clippings.Platform.Capture;

public sealed class WindowsGraphicsCaptureService
{
    private const uint D3D11CreateDeviceBgraSupport = 0x20;
    private const uint D3D11SdkVersion = 7;
    private const int D3DDriverTypeHardware = 1;
    private static readonly TimeSpan FrameTimeout = TimeSpan.FromSeconds(5);
    private static readonly Guid IdxgiDevice = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public bool IsSupported =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 18362) &&
        GraphicsCaptureSession.IsSupported();

    public async Task<SKBitmap> CaptureWindowAsync(
        IntPtr window,
        bool includeCursor = false,
        CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("目前的 Windows 版本不支援進階視窗擷取。");
        }

        using var device = CreateDevice();
        var item = CreateItem(window);
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            item.Size);
        using var session = framePool.CreateCaptureSession(item);
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        {
            session.IsCursorCaptureEnabled = includeCursor;
        }

        var frameArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            frameArrived.TrySetResult();
        }

        framePool.FrameArrived += OnFrameArrived;
        try
        {
            session.StartCapture();
            await frameArrived.Task.WaitAsync(FrameTimeout, cancellationToken).ConfigureAwait(false);
            framePool.FrameArrived -= OnFrameArrived;
            using var frame = framePool.TryGetNextFrame();
            return await CopyFrameAsync(frame).ConfigureAwait(false);
        }
        finally
        {
            framePool.FrameArrived -= OnFrameArrived;
        }
    }

    private static async Task<SKBitmap> CopyFrameAsync(Direct3D11CaptureFrame frame)
    {
        using var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
            frame.Surface,
            BitmapAlphaMode.Premultiplied);
        var length = checked(bitmap.PixelWidth * bitmap.PixelHeight * 4);
        var buffer = new Windows.Storage.Streams.Buffer((uint)length);
        bitmap.CopyToBuffer(buffer);

        var pixels = new byte[length];
        using (var reader = DataReader.FromBuffer(buffer))
        {
            reader.ReadBytes(pixels);
        }

        var result = new SKBitmap(new SKImageInfo(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul));
        pixels.CopyTo(result.GetPixelSpan());
        return result;
    }

    internal static GraphicsCaptureItem CreateItem(IntPtr window)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = IGraphicsCaptureItem;
        var result = interop.CreateForWindow(window, ref iid, out var itemPointer);
        Marshal.ThrowExceptionForHR(result);

        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    internal static GraphicsCaptureItem CreateItemForMonitor(IntPtr monitor)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = IGraphicsCaptureItem;
        var result = interop.CreateForMonitor(monitor, ref iid, out var itemPointer);
        Marshal.ThrowExceptionForHR(result);

        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    internal static IDirect3DDevice CreateDevice()
    {
        var result = D3D11CreateDevice(
            IntPtr.Zero,
            D3DDriverTypeHardware,
            IntPtr.Zero,
            D3D11CreateDeviceBgraSupport,
            IntPtr.Zero,
            0,
            D3D11SdkVersion,
            out var d3dDevice,
            out _,
            out var context);
        Marshal.ThrowExceptionForHR(result);

        IntPtr dxgiDevice = IntPtr.Zero;
        IntPtr inspectable = IntPtr.Zero;
        try
        {
            var iid = IdxgiDevice;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in iid, out dxgiDevice));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectable));
            return MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            ReleaseIfNotZero(inspectable);
            ReleaseIfNotZero(dxgiDevice);
            ReleaseIfNotZero(context);
            ReleaseIfNotZero(d3dDevice);
        }
    }

    private static void ReleaseIfNotZero(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            Marshal.Release(pointer);
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out IntPtr device,
        out int featureLevel,
        out IntPtr immediateContext);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);
}
