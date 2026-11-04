using System.Windows;
using Clippings.App.Views;
using Clippings.Platform.Interop;

namespace Clippings.App.Services;

public sealed class RecordingTargetPicker
{
    private readonly WindowEnumerationService _windows;

    public RecordingTargetPicker(WindowEnumerationService windows)
    {
        _windows = windows;
    }

    public WindowRecordingTarget? PickTarget()
    {
        var dialog = new RecordingTargetDialog(_windows);
        var owner = Application.Current?.MainWindow;
        if (owner?.IsVisible == true)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        return dialog.ShowDialog() == true ? dialog.SelectedTarget : null;
    }
}
