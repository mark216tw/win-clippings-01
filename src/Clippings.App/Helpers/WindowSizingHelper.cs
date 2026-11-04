using System.Windows;

namespace Clippings.App.Helpers;

public static class WindowSizingHelper
{
    private const double WorkAreaMargin = 32;

    public static void ClampToWorkArea(Window window)
    {
        var area = SystemParameters.WorkArea;
        var availableWidth = Math.Max(320, area.Width - WorkAreaMargin);
        var availableHeight = Math.Max(240, area.Height - WorkAreaMargin);

        window.MinWidth = Math.Min(window.MinWidth, availableWidth);
        window.MinHeight = Math.Min(window.MinHeight, availableHeight);
        window.Width = Math.Min(window.Width, availableWidth);
        window.Height = Math.Min(window.Height, availableHeight);
    }
}
