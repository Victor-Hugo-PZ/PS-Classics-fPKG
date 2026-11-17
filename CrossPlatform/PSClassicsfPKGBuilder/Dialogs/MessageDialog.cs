using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>Simple message and question dialogs with the look of the app (the Windows app uses message boxes).</summary>
public static class MessageDialog
{
    public static Task ShowInfo(Window owner, string title, string message) =>
        Show(owner, title, message, "OK", null);

    public static Task ShowError(Window owner, string title, string message) =>
        Show(owner, title, message, "OK", null, isError: true);

    /// <summary>Asks a yes/no question. Returns true for yes.</summary>
    public static async Task<bool> Confirm(Window owner, string title, string message, string yes = "Yes", string no = "No") =>
        await Show(owner, title, message, yes, no);

    private static async Task<bool> Show(Window owner, string title, string message, string primary, string secondary, bool isError = false)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        var text = new SelectableTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 20),
        };
        var heading = new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = isError ? new SolidColorBrush(Color.Parse("#FF99A4")) : Brushes.White,
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var primaryButton = new Button { Content = primary, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
        primaryButton.Classes.Add("accent");
        primaryButton.Click += (_, _) => dialog.Close(true);
        if (secondary != null)
        {
            var secondaryButton = new Button { Content = secondary, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
            secondaryButton.Click += (_, _) => dialog.Close(false);
            buttons.Children.Add(secondaryButton);
        }
        buttons.Children.Add(primaryButton);

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24),
            Children = { heading, text, buttons },
        };

        var result = await dialog.ShowDialog<bool?>(owner);
        return result == true;
    }
}
