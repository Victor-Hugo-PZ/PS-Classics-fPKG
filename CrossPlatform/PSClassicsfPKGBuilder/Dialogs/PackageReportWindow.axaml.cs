using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Material.Icons;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>Shows the result of <see cref="PackageVerifier.Verify"/> (same texts as the Windows app).</summary>
public partial class PackageReportWindow : Window
{
    private readonly PackageReport _report;
    private bool _showingToolOutput;

    public PackageReportWindow() : this(new PackageReport()) { }

    public PackageReportWindow(PackageReport report)
    {
        InitializeComponent();
        _report = report;

        if (report.IsHealthy)
        {
            ResultIcon.Kind = MaterialIconKind.CheckCircle;
            ResultIcon.Foreground = new SolidColorBrush(Color.Parse("#6CCB5F"));
            ResultTitle.Text = "No problems found";
            ResultSubtitle.Text = "The package is well formed. Sony certification rules that fake packages never meet are listed below and can be ignored.";
        }
        else
        {
            ResultIcon.Kind = MaterialIconKind.AlertCircle;
            ResultIcon.Foreground = new SolidColorBrush(Color.Parse("#FF99A4"));
            ResultTitle.Text = report.ProblemIssues.Count() + " problem(s) found";
            ResultSubtitle.Text = "These findings may stop the package from installing or running. Details below.";
        }

        ReportTextBox.Text = report.ToText();

        CopyButton.Click += async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(_showingToolOutput ? _report.ToolOutput : _report.ToText());
        };
        ToolOutputButton.Click += (_, _) =>
        {
            _showingToolOutput = !_showingToolOutput;
            ReportTextBox.Text = _showingToolOutput ? _report.ToolOutput : _report.ToText();
            ToolOutputButton.Content = _showingToolOutput ? "Show summary" : "Show tool output";
        };
        CloseButton.Click += (_, _) => Close();
    }
}
