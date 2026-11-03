using System.Text;

namespace Clippings.Platform.Interop;

public static class ConsoleBridge
{
    public static bool TryAttachToParent()
    {
        try
        {
            if (!NativeMethods.AttachConsole(NativeMethods.AttachParentProcess))
            {
                return false;
            }

            Console.OutputEncoding = Encoding.UTF8;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public static void WriteLine(string text)
    {
        try
        {
            Console.Out.WriteLine(text);
            Console.Out.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }
}
