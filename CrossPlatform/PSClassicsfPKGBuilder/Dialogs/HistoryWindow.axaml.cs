using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>List of the packages built on this computer, with the options used and how they run on the console (same as the Windows app).</summary>
public partial class HistoryWindow : Window
{
    private List<HistoryEntry> _entries = new();

    public HistoryWindow()
    {
        InitializeComponent();
        UiTranslator.Attach(this);
        foreach (var name in BuildHistory.ConsoleResultNames)
            ConsoleResultComboBox.Items.Add(Localizer.T(name));

        EntriesListBox.SelectionChanged += (_, _) => ShowSelected();
        SaveButton.Click += (_, _) =>
        {
            if (Selected is not { } entry) return;
            entry.ConsoleResult = (ConsoleResult)Math.Max(0, ConsoleResultComboBox.SelectedIndex);
            entry.Notes = NotesTextBox.Text ?? "";
            BuildHistory.AddOrUpdate(entry);
            Reload(entry.Id);
        };
        OpenFolderButton.Click += async (_, _) =>
        {
            if (Selected is not { } entry) return;
            if (Directory.Exists(entry.OutputFolder)) MainWindow.OpenFolder(entry.OutputFolder);
            else await MessageDialog.ShowInfo(this, Localizer.T("History"), Localizer.T("The folder of this package no longer exists."));
        };
        DeleteButton.Click += async (_, _) =>
        {
            if (Selected is not { } entry) return;
            if (await MessageDialog.Confirm(this, Localizer.T("History"), Localizer.T("Remove this entry from the history? The package file is not deleted.")))
            {
                BuildHistory.Remove(entry.Id);
                Reload(null);
            }
        };
        Reload(null);
    }

    private HistoryEntry Selected => (EntriesListBox.SelectedItem as Control)?.Tag as HistoryEntry;

    private static string TitleOf(HistoryEntry entry) => entry.Title == "" ? entry.NPTitle : entry.Title;

    private void Reload(string selectId)
    {
        _entries = BuildHistory.Load();
        EntriesListBox.Items.Clear();
        Control toSelect = null;
        foreach (var entry in _entries)
        {
            var status = entry.Succeeded ? Localizer.T(BuildHistory.ConsoleResultNames[(int)entry.ConsoleResult]) : Localizer.T("Build failed");
            var row = new StackPanel
            {
                Tag = entry,
                Margin = new Avalonia.Thickness(0, 4),
                Children =
                {
                    new TextBlock { Text = TitleOf(entry), FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = entry.Platform + " · " + entry.CreatedAt.ToString("g") + " · " + status, FontSize = 12, Opacity = 0.75 },
                },
            };
            EntriesListBox.Items.Add(row);
            if (entry.Id == selectId) toSelect = row;
        }
        EmptyText.IsVisible = _entries.Count == 0;
        EntriesListBox.SelectedItem = toSelect ?? (EntriesListBox.Items.Count > 0 ? EntriesListBox.Items[0] : null);
        ShowSelected();
    }

    private void ShowSelected()
    {
        var entry = Selected;
        DetailsPanel.IsEnabled = entry != null;
        if (entry == null)
        {
            DetailsTitle.Text = DetailsInfo.Text = DetailsSettings.Text = NotesTextBox.Text = "";
            return;
        }
        DetailsTitle.Text = TitleOf(entry);
        DetailsInfo.Text = entry.Platform + " · " + entry.NPTitle + " · " + entry.CreatedAt.ToString("g") + "\n" +
                           (entry.Succeeded ? (entry.PackagePath != "" ? entry.PackagePath : entry.OutputFolder) : Localizer.T("Build failed"));
        DetailsSettings.Text = entry.Settings;
        ConsoleResultComboBox.SelectedIndex = (int)entry.ConsoleResult;
        NotesTextBox.Text = entry.Notes;
    }
}
