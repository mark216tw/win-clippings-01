using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Clippings.App.Strings;

namespace Clippings.App.Helpers;

public static class FileActionHelper
{
    private const int ClipboardRetryCount = 5;

    public static bool TryRevealInExplorer(string? path, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = AppStrings.FileMissing;
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or IOException)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryCopyText(string text)
    {
        for (var attempt = 0; attempt < ClipboardRetryCount; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(30);
            }
        }

        return false;
    }
}
