using System.Windows;
using System.Windows.Controls;

namespace Clippings.App.Views;

public partial class RenameWorkspaceTabDialog : Window
{
    public RenameWorkspaceTabDialog(string title)
    {
        InitializeComponent();
        NameInput.Text = title;
    }

    public string TabName => NameInput.Text.Trim();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NameInput.Focus();
        NameInput.SelectAll();
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        if (ConfirmButton is not null)
        {
            ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(NameInput.Text);
        }
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NameInput.Text))
        {
            DialogResult = true;
        }
    }
}
