namespace Clippings.App.Services;

public interface INotificationService
{
    void Show(string message, bool isError = false, bool playSound = true);
}
