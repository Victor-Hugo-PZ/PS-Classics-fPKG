Imports System.Windows.Media

''' <summary>
''' Result of a build in plain words: "ready to install" (with the automatic package check) or what went wrong and
''' how to fix it (<see cref="ErrorExplainer"/>), with the technical details below.
''' </summary>
Public Class BuildResultWindow

    Private ReadOnly _PackagePath As String
    Private ReadOnly _OutputFolder As String
    Private _FixAction As FixAction = FixAction.None

    ''' <summary>The fix the user chose with the button (FixAction.None when the window was just closed).</summary>
    Public ReadOnly Property RequestedAction As FixAction = FixAction.None

    Public Sub New(Result As PackageBuildResult, OutputFolder As String, Report As PackageReport, ErrorDetails As String)
        InitializeComponent()
        _PackagePath = If(Result?.PackagePath, "")
        _OutputFolder = OutputFolder

        If Result IsNot Nothing AndAlso Result.Success Then
            If Report Is Nothing OrElse Report.IsHealthy Then
                ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
                ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&H6C, &HCB, &H5F))
                ResultTitle.Text = Localizer.T("Your package is ready")
                ResultText.Text = If(Report Is Nothing,
                                     Localizer.T("The package was created. Copy it to the console to install it."),
                                     Localizer.T("The package was created and the quick check found no problems. Copy it to the console to install it."))
            Else
                ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24
                ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&HFF, &HC8, &H57))
                ResultTitle.Text = Localizer.T("Package created, but the check found problems")
                ResultText.Text = Localizer.T("The package may not install or run. See the details below.")
            End If
            SuggestionText.Text = Localizer.T("Tip: write in the History how the game runs on the console. It helps to compare emulator settings.")
            PackageText.Text = If(_PackagePath <> "", Localizer.F("Package: {0}", _PackagePath), Localizer.F("Output folder: {0}", OutputFolder))
            DetailsTextBox.Text = If(Report?.ToText(), Result.Output)
        Else
            Dim Friendly As FriendlyError = ErrorExplainer.Explain(ErrorDetails)
            ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ErrorCircle24
            ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&HFF, &H99, &HA4))
            ResultTitle.Text = Friendly.Title
            ResultText.Text = Friendly.Explanation
            SuggestionText.Text = "→ " + Friendly.Suggestion
            PackageText.Text = If(Friendly.KeyLine <> "", Localizer.F("Error: {0}", Friendly.KeyLine), "")
            DetailsHeader.Text = Localizer.T("Technical details")
            DetailsTextBox.Text = ErrorDetails
            FullCheckButton.Visibility = Visibility.Collapsed
            InstallGuideButton.Visibility = Visibility.Collapsed
            'A button for the fix (the Wine setup only exists on Linux and macOS)
            If Friendly.Action <> FixAction.None AndAlso Not (Friendly.Action = FixAction.WineSetup AndAlso Platform.IsWindows) Then
                _FixAction = Friendly.Action
                FixButton.Content = Friendly.ActionLabel
                FixButton.Visibility = Visibility.Visible
            End If
        End If

        If _PackagePath = "" Then FullCheckButton.Visibility = Visibility.Collapsed
        UiTranslator.Attach(Me)
    End Sub

    Private Sub FixButton_Click(sender As Object, e As RoutedEventArgs) Handles FixButton.Click
        _RequestedAction = _FixAction
        Close()
    End Sub

    Private Sub CopyButton_Click(sender As Object, e As RoutedEventArgs) Handles CopyButton.Click
        Clipboard.SetText(ResultTitle.Text + vbCrLf + ResultText.Text + vbCrLf + SuggestionText.Text + vbCrLf + vbCrLf + DetailsTextBox.Text)
    End Sub

    Private Async Sub FullCheckButton_Click(sender As Object, e As RoutedEventArgs) Handles FullCheckButton.Click
        FullCheckButton.IsEnabled = False
        FullCheckButton.Content = Localizer.T("Checking…")
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait
        Try
            Dim PkgFile As String = _PackagePath
            Dim Report As PackageReport = Await Task.Run(Function() PackageVerifier.Verify(PkgFile))
            Mouse.OverrideCursor = Nothing
            Dim ReportWindow As New PackageReportWindow(Report) With {.Owner = Me}
            ReportWindow.ShowDialog()
        Catch ex As Exception
            Mouse.OverrideCursor = Nothing
            MsgBox(Localizer.F("The package could not be checked:\n{0}", ex.Message), MsgBoxStyle.Critical, Localizer.T("Check a .pkg"))
        Finally
            Mouse.OverrideCursor = Nothing
            FullCheckButton.Content = Localizer.T("Full check")
            FullCheckButton.IsEnabled = True
        End Try
    End Sub

    Private Sub InstallGuideButton_Click(sender As Object, e As RoutedEventArgs) Handles InstallGuideButton.Click
        Dim Guide As New GuideWindow() With {.Owner = Me}
        Guide.ShowInstallSection()
        Guide.ShowDialog()
    End Sub

    Private Sub OpenFolderButton_Click(sender As Object, e As RoutedEventArgs) Handles OpenFolderButton.Click
        Dim Arguments As String = If(_PackagePath <> "" AndAlso IO.File.Exists(_PackagePath), "/select,""" + _PackagePath + """", """" + _OutputFolder + """")
        Process.Start(New ProcessStartInfo("explorer", Arguments) With {.UseShellExecute = True})
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        Close()
    End Sub

End Class
