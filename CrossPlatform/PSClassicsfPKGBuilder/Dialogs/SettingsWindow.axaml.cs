using Avalonia.Controls;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Dialogs;

/// <summary>Preferences (same as the Windows app). Changes apply and are saved at once.</summary>
public partial class SettingsWindow : Window
{
    private readonly bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        var settings = AppSettings.Current;
        foreach (var language in Localizer.Languages)
            LanguageComboBox.Items.Add(language.Value);
        LanguageComboBox.SelectedIndex = Math.Max(0, Localizer.Languages.ToList().FindIndex(l => l.Key == Localizer.Language));
        GuidedModeCheckBox.IsChecked = settings.GuidedMode;
        CheckPackageCheckBox.IsChecked = settings.CheckPackageAfterBuild;
        CheckUpdatesCheckBox.IsChecked = settings.CheckForUpdates;

        GuidedModeCheckBox.IsCheckedChanged += (_, _) => Save();
        CheckPackageCheckBox.IsCheckedChanged += (_, _) => Save();
        CheckUpdatesCheckBox.IsCheckedChanged += (_, _) => Save();
        LanguageComboBox.SelectionChanged += (_, _) =>
        {
            if (_loading || LanguageComboBox.SelectedIndex < 0) return;
            var code = Localizer.Languages[LanguageComboBox.SelectedIndex].Key;
            AppSettings.Current.Language = code;
            AppSettings.Current.Save();
            Localizer.Language = code;
        };
        CloseButton.Click += (_, _) => Close();
        UiTranslator.Attach(this);
        _loading = false;
    }

    private void Save()
    {
        if (_loading) return;
        var settings = AppSettings.Current;
        settings.GuidedMode = GuidedModeCheckBox.IsChecked == true;
        settings.CheckPackageAfterBuild = CheckPackageCheckBox.IsChecked == true;
        settings.CheckForUpdates = CheckUpdatesCheckBox.IsChecked == true;
        settings.Save();
    }
}
