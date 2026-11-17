Imports System.Windows.Media
Imports Wpf.Ui.Appearance

Class Application

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)

        'Language of the user interface: saved choice or the system language
        Localizer.Language = AppSettings.Current.EffectiveLanguage()

        'PlayStation blue as accent color of the dark theme
        ApplicationAccentColorManager.Apply(Color.FromRgb(&H0, &H70, &HD1), ApplicationTheme.Dark)
    End Sub

End Class
