using Avalonia.Controls;
using Avalonia.Media;
using PSClassicsfPKGBuilder.Dialogs;

namespace PSClassicsfPKGBuilder.Controls;

/// <summary>Label of a form row with a help button and a "Required" / "Optional" badge. Set LabelText, HelpText and IsRequired in XAML.</summary>
public partial class FieldLabel : UserControl
{
    private string _labelText = "";
    private string _helpText = "";
    private bool _isRequired;

    public FieldLabel()
    {
        InitializeComponent();
        IsRequired = false;
        HelpButton.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
                await MessageDialog.ShowInfo(owner, _labelText, _helpText);
        };
    }

    public string LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? "";
            LabelTextBlock.Text = _labelText;
        }
    }

    /// <summary>Explanation shown in the tooltip and in the dialog opened by the help button.</summary>
    public string HelpText
    {
        get => _helpText;
        set
        {
            _helpText = value ?? "";
            ToolTip.SetTip(HelpButton, _helpText);
        }
    }

    public bool IsRequired
    {
        get => _isRequired;
        set
        {
            _isRequired = value;
            BadgeText.Text = value ? "Required" : "Optional";
            BadgeText.Foreground = value ? Brushes.Black : new SolidColorBrush(Color.Parse("#C5C5C5"));
            BadgeBorder.Background = new SolidColorBrush(Color.Parse(value ? "#5AA8F2" : "#3A3A3A"));
        }
    }
}
