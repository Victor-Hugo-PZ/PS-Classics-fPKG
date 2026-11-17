''' <summary>Preferences: language, step-by-step mode, package check after building, update check. Changes apply and are saved at once.</summary>
Public Class SettingsWindow

    Private _Loading As Boolean = True

    Public Sub New()
        InitializeComponent()
        Dim Settings As AppSettings = AppSettings.Current
        For Each Item In Localizer.Languages
            LanguageComboBox.Items.Add(Item.Value)
        Next
        LanguageComboBox.SelectedIndex = Math.Max(0, Localizer.Languages.ToList().FindIndex(Function(Item) Item.Key = Localizer.Language))
        GuidedModeCheckBox.IsChecked = Settings.GuidedMode
        CheckPackageCheckBox.IsChecked = Settings.CheckPackageAfterBuild
        CheckUpdatesCheckBox.IsChecked = Settings.CheckForUpdates
        UiTranslator.Attach(Me)
        _Loading = False
    End Sub

    Private Sub SaveSettings() Handles GuidedModeCheckBox.Click, CheckPackageCheckBox.Click, CheckUpdatesCheckBox.Click
        If _Loading Then Return
        Dim Settings As AppSettings = AppSettings.Current
        Settings.GuidedMode = GuidedModeCheckBox.IsChecked = True
        Settings.CheckPackageAfterBuild = CheckPackageCheckBox.IsChecked = True
        Settings.CheckForUpdates = CheckUpdatesCheckBox.IsChecked = True
        Settings.Save()
    End Sub

    Private Sub LanguageComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles LanguageComboBox.SelectionChanged
        If _Loading OrElse LanguageComboBox.SelectedIndex < 0 Then Return
        Dim Code As String = Localizer.Languages(LanguageComboBox.SelectedIndex).Key
        AppSettings.Current.Language = Code
        AppSettings.Current.Save()
        Localizer.Language = Code
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        Close()
    End Sub

End Class
