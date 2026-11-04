using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clippings.Platform.Interop;

namespace Clippings.App.Views;

public partial class RecordingTargetDialog : Window
{
    private readonly WindowEnumerationService _windows;

    public RecordingTargetDialog(WindowEnumerationService windows)
    {
        _windows = windows;
        InitializeComponent();
        RefreshTargets();
    }

    public WindowRecordingTarget? SelectedTarget { get; private set; }

    private void RefreshTargets()
    {
        var targets = _windows.GetRecordingTargets();
        TargetList.ItemsSource = targets;
        TargetList.SelectedIndex = targets.Count > 0 ? 0 : -1;
        EmptyText.Visibility = targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TargetList.Visibility = targets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => ConfirmButton.IsEnabled = TargetList.SelectedItem is WindowRecordingTarget;

    private void OnTargetDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TargetList.SelectedItem is WindowRecordingTarget)
        {
            ConfirmSelection();
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshTargets();

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnConfirmClick(object sender, RoutedEventArgs e) => ConfirmSelection();

    private void ConfirmSelection()
    {
        if (TargetList.SelectedItem is not WindowRecordingTarget selected)
        {
            return;
        }

        SelectedTarget = selected;
        DialogResult = true;
    }
}
