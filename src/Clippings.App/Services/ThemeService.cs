using System.Windows;
using Clippings.Core.Models;
using Clippings.Platform.Interop;
using Microsoft.Win32;

namespace Clippings.App.Services;

public sealed class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private ResourceDictionary? _current;
    private bool _isDark;

    public bool IsDark => _isDark;

    public void Apply(AppTheme theme)
    {
        var resolvedTheme = theme == AppTheme.System
            ? IsSystemDark() ? AppTheme.Dark : AppTheme.Light
            : theme;
        var (source, dark) = resolvedTheme switch
        {
            AppTheme.Dark => ("Styles/Dark.xaml", true),
            AppTheme.WarmPaper => ("Styles/WarmPaper.xaml", false),
            AppTheme.SoftSage => ("Styles/SoftSage.xaml", false),
            AppTheme.DuskBlue => ("Styles/DuskBlue.xaml", true),
            AppTheme.DuskPurple => ("Styles/DuskPurple.xaml", true),
            AppTheme.InkGreenNight => ("Styles/InkGreenNight.xaml", true),
            AppTheme.MistBlueMorning => ("Styles/MistBlueMorning.xaml", false),
            _ => ("Styles/Light.xaml", false),
        };

        _isDark = dark;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative),
        };

        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current is not null)
        {
            merged.Remove(_current);
        }

        merged.Insert(0, dictionary);
        _current = dictionary;

        foreach (Window window in Application.Current.Windows)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            WindowHelper.ApplyDarkTitleBar(handle, dark);
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
