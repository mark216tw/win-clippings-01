using Microsoft.Win32;

namespace Clippings.Platform.Storage;

public static class StartupRegistrar
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clippings";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && value.Length > 0;
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (enabled && string.IsNullOrWhiteSpace(executable))
            {
                return false;
            }

            var command = enabled ? $"\"{executable}\" --autostart" : null;
            using (var currentKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false))
            {
                if (string.Equals(currentKey?.GetValue(ValueName) as string, command, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            key.SetValue(ValueName, command!);
            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return false;
        }
    }
}
