using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clippings.App.Models;
using Clippings.App.Strings;

namespace Clippings.App.Views;

public partial class WorkspaceTabStrip : UserControl
{
    public WorkspaceTabStrip()
    {
        InitializeComponent();
    }

    public event EventHandler? TabSelectionChanged;

    public event EventHandler<WorkspaceTab>? TabCloseRequested;

    public event EventHandler? CloseAllRequested;

    public IEnumerable? ItemsSource
    {
        get => TabList.ItemsSource;
        set => TabList.ItemsSource = value;
    }

    public WorkspaceTab? SelectedTab
    {
        get => TabList.SelectedItem as WorkspaceTab;
        set => TabList.SelectedItem = value;
    }

    public void SetCloseAllEnabled(bool enabled) => CloseAllButton.IsEnabled = enabled;

    private void OnCloseAllClick(object sender, RoutedEventArgs e)
        => CloseAllRequested?.Invoke(this, EventArgs.Empty);

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
        => TabSelectionChanged?.Invoke(this, EventArgs.Empty);

    private void OnTabContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: WorkspaceTab tab, ContextMenu: { } menu })
        {
            return;
        }

        var renameItem = new MenuItem
        {
            Header = AppStrings.WorkspaceRenameTab,
            DataContext = tab,
        };
        System.Windows.Automation.AutomationProperties.SetName(renameItem, AppStrings.WorkspaceRenameTab);
        renameItem.Click += OnRenameTabClick;
        menu.Items.Clear();
        menu.Items.Add(renameItem);
    }

    private void OnRenameTabClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WorkspaceTab tab)
        {
            return;
        }

        var dialog = new RenameWorkspaceTabDialog(tab.Title)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true)
        {
            tab.Title = dialog.TabName;
        }
    }

    private void OnCloseTabPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        e.Handled = true;
        if ((sender as FrameworkElement)?.DataContext is WorkspaceTab tab)
        {
            TabCloseRequested?.Invoke(this, tab);
        }
    }
}
