using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clippings.App.Strings;

namespace Clippings.App.Helpers;

public static class TextBoxContextMenuHelper
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnTextBoxLoaded));
        EventManager.RegisterClassHandler(
            typeof(TextBox),
            FrameworkElement.ContextMenuOpeningEvent,
            new ContextMenuEventHandler(OnContextMenuOpening));
    }

    private static void OnTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (textBox.ContextMenu is null)
        {
            textBox.ContextMenu = CreateMenu(textBox);
        }
        else
        {
            ApplyStyles(textBox.ContextMenu);
        }
    }

    private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (textBox.ContextMenu is null)
        {
            textBox.ContextMenu = CreateMenu(textBox);
        }

        ApplyStyles(textBox.ContextMenu);
    }

    private static ContextMenu CreateMenu(TextBox textBox)
    {
        var menu = new ContextMenu();
        ApplyStyle(menu, "AppContextMenuStyle");
        menu.Items.Add(CreateItem(AppStrings.ActionUndo, "Ctrl+Z", ApplicationCommands.Undo, textBox));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateItem(AppStrings.ActionCut, "Ctrl+X", ApplicationCommands.Cut, textBox));
        menu.Items.Add(CreateItem(AppStrings.ActionCopy, "Ctrl+C", ApplicationCommands.Copy, textBox));
        menu.Items.Add(CreateItem(AppStrings.ActionPaste, "Ctrl+V", ApplicationCommands.Paste, textBox));
        menu.Items.Add(CreateSeparator());
        menu.Items.Add(CreateItem(AppStrings.ActionSelectAll, "Ctrl+A", ApplicationCommands.SelectAll, textBox));
        return menu;
    }

    private static MenuItem CreateItem(
        string header,
        string gesture,
        RoutedCommand command,
        TextBox textBox)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = gesture,
            Command = command,
            CommandTarget = textBox,
        };
        ApplyStyle(item, "AppMenuItemStyle");
        return item;
    }

    private static Separator CreateSeparator()
    {
        var separator = new Separator();
        ApplyStyle(separator, "AppSeparatorStyle");
        return separator;
    }

    private static void ApplyStyles(ContextMenu menu)
    {
        ApplyStyle(menu, "AppContextMenuStyle");
        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            ApplyStyle(item, "AppMenuItemStyle");
        }

        foreach (var separator in menu.Items.OfType<Separator>())
        {
            ApplyStyle(separator, "AppSeparatorStyle");
        }
    }

    private static void ApplyStyle(FrameworkElement element, string key)
    {
        if (Application.Current?.TryFindResource(key) is Style style)
        {
            element.Style = style;
        }
    }
}
