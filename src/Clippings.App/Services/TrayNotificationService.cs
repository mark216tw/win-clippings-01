using System.Windows;
using Clippings.App.Strings;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Clippings.App.Services;

public sealed class TrayNotificationService : INotificationService
{
    private TaskbarIcon? _trayIcon;

    public void Initialize(TaskbarIcon? trayIcon) => _trayIcon = trayIcon;

    public void Show(string message, bool isError = false, bool playSound = true)
    {
        if (_trayIcon is null)
        {
            return;
        }

        void ShowCore()
        {
            var trayIcon = _trayIcon;
            if (trayIcon is null)
            {
                return;
            }

            trayIcon.ShowNotification(
                AppStrings.AppName,
                message,
                isError ? NotificationIcon.Error : NotificationIcon.Info,
                sound: playSound);
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ShowCore();
        }
        else
        {
            _ = dispatcher.BeginInvoke(ShowCore);
        }
    }
}
