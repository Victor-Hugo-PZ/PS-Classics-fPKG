using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>
/// Builds several games one after the other from .zip or .7z files, with no questions (same as the Windows app, see <see cref="BatchBuilder"/>).
/// Archives can be dropped on the window or added with the button.
/// </summary>
public partial class QueueWindow : Window
{
    private readonly BatchQueue _queue = new();
    private CancellationTokenSource _cancel;
    private bool _running;

    public QueueWindow()
    {
        InitializeComponent();
        foreach (var preset in EmulatorPresets.PS1)
            PresetComboBox.Items.Add(Localizer.T(preset.Name));
        PresetComboBox.SelectedIndex = 0;
        // The settings of one game: the first entry gives it back to "Settings for all games"
        GamePresetComboBox.Items.Add(Localizer.T("Same as all games"));
        foreach (var name in EmulatorPresets.AllNames())
            GamePresetComboBox.Items.Add(Localizer.T(name));
        GamePresetComboBox.SelectedIndex = 0;
        OutputFolderTextBox.Text = AppSettings.Current.QueueOutputFolder;

        AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles();
            if (files == null) return;
            e.Handled = true;
            AddArchives(files.Select(item => item.TryGetLocalPath()).Where(path => !string.IsNullOrEmpty(path)));
        });

        AddButton.Click += async (_, _) =>
        {
            var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Localizer.T("Select the game archives"),
                AllowMultiple = true,
                FileTypeFilter = new[] { new FilePickerFileType("ZIP / 7Z") { Patterns = new[] { "*.zip", "*.7z", "*.ZIP", "*.7Z" } } },
            });
            AddArchives(picked.Select(file => file.TryGetLocalPath()).Where(path => !string.IsNullOrEmpty(path)));
        };
        RemoveButton.Click += (_, _) =>
        {
            var indexes = ItemsListBox.Selection.SelectedIndexes.OrderByDescending(index => index).ToList();
            foreach (var index in indexes)
                if (index >= 0 && index < _queue.Items.Count) _queue.Items.RemoveAt(index);
            RefreshList();
        };
        ClearDoneButton.Click += (_, _) =>
        {
            _queue.ClearDone();
            RefreshList();
        };
        BrowseOutputButton.Click += async (_, _) =>
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Localizer.T("Please select an output folder"), AllowMultiple = false });
            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (!string.IsNullOrEmpty(path)) OutputFolderTextBox.Text = path;
        };
        ApplyPresetButton.Click += async (_, _) =>
        {
            var games = ItemsListBox.Selection.SelectedIndexes.Where(index => index >= 0 && index < _queue.Items.Count).Select(index => _queue.Items[index]).ToList();
            if (games.Count == 0)
            {
                await MessageDialog.ShowInfo(this, Localizer.T("Queue"), Localizer.T("Select the games in the list first (Ctrl or Shift to select several)."));
                return;
            }
            var names = EmulatorPresets.AllNames();
            var index = GamePresetComboBox.SelectedIndex;
            _queue.SetPreset(games, index <= 0 || index > names.Count ? "" : names[index - 1]);
            RefreshList();
        };
        OpenOutputButton.Click += async (_, _) =>
        {
            var folder = OutputFolderTextBox.Text;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                await MessageDialog.ShowInfo(this, Localizer.T("Queue"), Localizer.T("The output folder does not exist yet."));
                return;
            }
            MainWindow.OpenFolder(folder);
        };
        StartButton.Click += async (_, _) => await Start();
        CancelButton.Click += (_, _) =>
        {
            _cancel?.Cancel();
            CancelButton.IsEnabled = false;
        };
        CloseButton.Click += (_, _) => Close();
        Closing += async (_, e) =>
        {
            if (!_running) return;
            e.Cancel = true;
            await MessageDialog.ShowInfo(this, Localizer.T("Queue"), Localizer.T("The queue is still building. Press 'Stop after this game' and wait before closing."));
        };

        RefreshList();
        UiTranslator.Attach(this);
    }

    /// <summary>Adds archives to the queue.</summary>
    public void AddArchives(IEnumerable<string> files)
    {
        _queue.Add(files);
        RefreshList();
    }

    private void RefreshList()
    {
        // The rows are rebuilt on every change, so the selection is kept by position
        var selected = ItemsListBox.Selection.SelectedIndexes.ToList();
        ItemsListBox.Items.Clear();
        foreach (var item in _queue.Items) ItemsListBox.Items.Add(item.Describe());
        foreach (var index in selected)
            if (index >= 0 && index < ItemsListBox.Items.Count) ItemsListBox.Selection.Select(index);
        EmptyText.IsVisible = _queue.Items.Count == 0;
        SummaryText.Text = _queue.Items.Count == 0 ? "" : _queue.Summary();
        var idle = !_running;
        StartButton.IsEnabled = idle && _queue.ToBuild().Count > 0;
        AddButton.IsEnabled = idle;
        RemoveButton.IsEnabled = idle;
        ClearDoneButton.IsEnabled = idle;
        BrowseOutputButton.IsEnabled = idle;
        PresetComboBox.IsEnabled = idle;
        GamePresetComboBox.IsEnabled = idle;
        ApplyPresetButton.IsEnabled = idle;
        OnlineCheckBox.IsEnabled = idle;
    }

    private async Task Start()
    {
        if (string.IsNullOrWhiteSpace(OutputFolderTextBox.Text))
        {
            await MessageDialog.ShowInfo(this, Localizer.T("Queue"), Localizer.T("Choose the output folder first."));
            return;
        }
        try
        {
            Directory.CreateDirectory(OutputFolderTextBox.Text);
        }
        catch (Exception ex)
        {
            await MessageDialog.ShowInfo(this, Localizer.T("Queue"), Localizer.F("The output folder cannot be used: {0}", ex.Message));
            return;
        }

        _queue.ResetFailed();
        var online = OnlineCheckBox.IsChecked == true;
        var options = new BatchOptions
        {
            OutputFolder = OutputFolderTextBox.Text,
            PresetName = EmulatorPresets.PS1[Math.Max(0, PresetComboBox.SelectedIndex)].Name,
            FindCovers = online,
            FindTitles = online,
            Report = _ => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshList),
        };
        AppSettings.Current.QueueOutputFolder = OutputFolderTextBox.Text;
        AppSettings.Current.Save();

        _running = true;
        _cancel = new CancellationTokenSource();
        var token = _cancel.Token;
        StartButton.IsVisible = false;
        CancelButton.IsVisible = true;
        CancelButton.IsEnabled = true;
        QueueProgress.IsVisible = true;
        RefreshList();

        var toBuild = _queue.ToBuild();
        try
        {
            await Task.Run(() => BatchBuilder.BuildAll(toBuild, options, token));
        }
        finally
        {
            _running = false;
            StartButton.IsVisible = true;
            CancelButton.IsVisible = false;
            QueueProgress.IsVisible = false;
            RefreshList();
        }
    }
}
