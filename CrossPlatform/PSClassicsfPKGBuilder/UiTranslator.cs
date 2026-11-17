using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using PS_Classics_fPKG_Builder;
using PSClassicsfPKGBuilder.Controls;

namespace PSClassicsfPKGBuilder;

/// <summary>
/// Translates the texts of a window (labels, buttons, hints, tooltips, placeholders) with <see cref="Localizer"/>,
/// like the Windows app. The XAML stays in English; calling it again after the language changed switches the texts.
/// Combo box items keep their values (they are option values) and the home screen preview imitates the console.
/// </summary>
public static class UiTranslator
{
    public static void Translate(ILogical root)
    {
        switch (root)
        {
            case null:
            case DashPreview:
            case ComboBoxItem:
                return;
            case FieldLabel label:
                label.LabelText = Localizer.TranslateProperty(label, "LabelText", label.LabelText);
                label.HelpText = Localizer.TranslateProperty(label, "HelpText", label.HelpText);
                label.IsRequired = label.IsRequired;
                return;
        }

        if (root is Window window)
            window.Title = Localizer.TranslateProperty(window, "Title", window.Title);
        if (root is TextBlock text && text is not SelectableTextBlock { Name: "InstructionsText" })
            text.Text = Localizer.TranslateProperty(text, "Text", text.Text);
        if (root is TextBox box)
            box.PlaceholderText = Localizer.TranslateProperty(box, "PlaceholderText", box.PlaceholderText);
        if (root is HeaderedContentControl { Header: string header } headered)
            headered.Header = Localizer.TranslateProperty(headered, "Header", header);
        if (root is ContentControl { Content: string content } contentControl and not Window)
            contentControl.Content = Localizer.TranslateProperty(contentControl, "Content", content);
        if (root is Control control && ToolTip.GetTip(control) is string tip)
            ToolTip.SetTip(control, Localizer.TranslateProperty(control, "ToolTip", tip));

        foreach (var child in root.LogicalChildren.ToList())
            Translate(child);
    }

    /// <summary>Translates a window now and every time the language changes, until it is closed.</summary>
    public static void Attach(Window window)
    {
        Translate(window);
        EventHandler handler = (_, _) => Dispatcher.UIThread.Post(() => Translate(window));
        Localizer.LanguageChanged += handler;
        window.Closed += (_, _) => Localizer.LanguageChanged -= handler;
    }
}
