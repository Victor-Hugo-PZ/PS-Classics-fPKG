using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Material.Icons;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>
/// Result of a build in plain words (same as the Windows app): "ready to install" with the automatic package check, or
/// what went wrong and how to fix it (<see cref="ErrorExplainer"/>), with the technical details below.
/// </summary>
public partial class BuildResultWindow : Window
{
    private readonly string _packagePath = "";
    private readonly string _outputFolder = "";

    public BuildResultWindow() => InitializeComponent();

    public BuildResultWindow(PackageBuildResult result, string outputFolder, PackageReport report, string errorDetails)
    {
        InitializeComponent();
        _packagePath = result?.PackagePath ?? "";
        _outputFolder = outputFolder;

        if (result is { Success: true })
        {
            if (report == null || report.IsHealthy)
            {
                ResultIcon.Kind = MaterialIconKind.CheckCircle;
                ResultIcon.Foreground = new SolidColorBrush(Color.Parse("#6CCB5F"));
                ResultTitle.Text = Localizer.T("Your package is ready");
                ResultText.Text = report == null
                    ? Localizer.T("The package was created. Copy it to the console to install it.")
                    : Localizer.T("The package was created and the quick check found no problems. Copy it to the console to install it.");
            }
            else
            {
                ResultIcon.Kind = MaterialIconKind.Alert;
                ResultIcon.Foreground = new SolidColorBrush(Color.Parse("#FFC857"));
                ResultTitle.Text = Localizer.T("Package created, but the check found problems");
                ResultText.Text = Localizer.T("The package may not install or run. See the details below.");
            }
            SuggestionText.Text = Localizer.T("Tip: write in the History how the game runs on the console. It helps to compare emulator settings.");
            PackageText.Text = _packagePath != "" ? Localizer.F("Package: {0}", _packagePath) : Localizer.F("Output folder: {0}", outputFolder);
            DetailsTextBox.Text = report?.ToText() ?? result.Output;
        }
        else
        {
            var friendly = ErrorExplainer.Explain(errorDetails);
            ResultIcon.Kind = MaterialIconKind.CloseCircle;
            ResultIcon.Foreground = new SolidColorBrush(Color.Parse("#FF99A4"));
            ResultTitle.Text = friendly.Title;
            ResultText.Text = friendly.Explanation;
            SuggestionText.Text = "→ " + friendly.Suggestion;
            PackageText.Text = friendly.KeyLine != "" ? Localizer.F("Error: {0}", friendly.KeyLine) : "";
            DetailsHeader.Text = Localizer.T("Technical details");
            DetailsTextBox.Text = errorDetails;
            FullCheckButton.IsVisible = false;
            InstallGuideButton.IsVisible = false;
        }
        if (_packagePath == "") FullCheckButton.IsVisible = false;

        CopyButton.Click += async (_, _) =>
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
                await clipboard.SetTextAsync(ResultTitle.Text + "\n" + ResultText.Text + "\n" + SuggestionText.Text + "\n\n" + DetailsTextBox.Text);
        };
        FullCheckButton.Click += async (_, _) =>
        {
            FullCheckButton.IsEnabled = false;
            FullCheckButton.Content = Localizer.T("Checking…");
            try
            {
                var pkg = _packagePath;
                var fullReport = await Task.Run(() => PackageVerifier.Verify(pkg));
                await new PackageReportWindow(fullReport).ShowDialog(this);
            }
            catch (Exception ex)
            {
                await MessageDialog.ShowError(this, Localizer.T("Check a .pkg"), Localizer.F("The package could not be checked:\n{0}", ex.Message));
            }
            finally
            {
                FullCheckButton.Content = Localizer.T("Full check");
                FullCheckButton.IsEnabled = true;
            }
        };
        InstallGuideButton.Click += async (_, _) =>
        {
            var guide = new GuideWindow();
            guide.ShowInstallSection();
            await guide.ShowDialog(this);
        };
        OpenFolderButton.Click += (_, _) => MainWindow.OpenFolder(_outputFolder);
        CloseButton.Click += (_, _) => Close();
        UiTranslator.Attach(this);
    }
}
