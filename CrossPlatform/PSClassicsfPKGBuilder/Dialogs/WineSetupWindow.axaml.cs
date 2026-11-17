using Avalonia.Controls;
using Avalonia.Threading;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>Checks Wine and prepares the app's Wine prefix (wineboot + winetricks vcrun2008).</summary>
public partial class WineSetupWindow : Window
{
    private bool _running;

    public WineSetupWindow()
    {
        InitializeComponent();
        InstructionsText.Text = Wine.InstallInstructions();
        RefreshButton.Click += (_, _) => RefreshStatus();
        CloseButton.Click += (_, _) => Close();
        SetUpButton.Click += async (_, _) => await SetUp();
        RefreshStatus();
        UiTranslator.Attach(this);
    }

    private static string Line(bool ok, string text) => (ok ? "✔  " : "✘  ") + text;

    private void RefreshStatus()
    {
        var status = Wine.GetStatus();
        WineLine.Text = Line(status.WineFound, status.WineFound ? "Wine: " + status.Executable : Localizer.T("Wine: not found"));
        WinetricksLine.Text = Line(status.WinetricksExecutable != null,
            status.WinetricksExecutable != null ? "winetricks: " + status.WinetricksExecutable : Localizer.T("winetricks: not found"));
        PrefixLine.Text = Line(status.PrefixReady, Localizer.F("Wine folder of the app: {0}", Wine.PrefixDirectory));
        RuntimeLine.Text = Line(status.Vc2008Installed, Localizer.T("Visual C++ 2008 runtime (needed by the tools)"));
        SetUpButton.IsEnabled = !_running && status.WineFound && !status.IsReady;
        SetUpButton.Content = status.IsReady ? Localizer.T("Ready") : Localizer.T("Set up Wine");
    }

    private async Task SetUp()
    {
        _running = true;
        SetUpButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        LogTextBox.Text = "";
        void Log(string message) => Dispatcher.UIThread.Post(() =>
        {
            LogTextBox.Text += message + Environment.NewLine;
            LogTextBox.CaretIndex = LogTextBox.Text.Length;
        });

        bool ok;
        try
        {
            ok = await Task.Run(() => Wine.SetUp(Log));
        }
        catch (Exception ex)
        {
            Log(ex.Message);
            ok = false;
        }

        Log(ok ? Localizer.T("Wine is ready.") : Localizer.T("The setup did not finish. Check the messages above."));
        _running = false;
        RefreshButton.IsEnabled = true;
        RefreshStatus();
    }
}
