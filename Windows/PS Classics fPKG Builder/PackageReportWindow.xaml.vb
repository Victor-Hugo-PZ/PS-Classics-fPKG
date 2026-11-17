Imports System.Windows.Media

''' <summary>Shows the result of <see cref="PackageVerifier.Verify"/>.</summary>
Public Class PackageReportWindow

    Private ReadOnly _Report As PackageReport
    Private _ShowingToolOutput As Boolean

    Public Sub New(Report As PackageReport)
        InitializeComponent()
        _Report = Report

        If Report.IsHealthy Then
            ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
            ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&H6C, &HCB, &H5F))
            ResultTitle.Text = Localizer.T("No problems found")
            ResultSubtitle.Text = Localizer.T("The package is well formed. Sony certification rules that fake packages never meet are listed below and can be ignored.")
        Else
            ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ErrorCircle24
            ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&HFF, &H99, &HA4))
            ResultTitle.Text = Localizer.F("{0} problem(s) found", Report.ProblemIssues.Count())
            ResultSubtitle.Text = Localizer.T("These findings may stop the package from installing or running. Details below.")
        End If

        ReportTextBox.Text = Report.ToText()
        UiTranslator.Attach(Me)
    End Sub

    Private Sub CopyButton_Click(sender As Object, e As RoutedEventArgs) Handles CopyButton.Click
        Clipboard.SetText(If(_ShowingToolOutput, _Report.ToolOutput, _Report.ToText()))
    End Sub

    Private Sub ToolOutputButton_Click(sender As Object, e As RoutedEventArgs) Handles ToolOutputButton.Click
        _ShowingToolOutput = Not _ShowingToolOutput
        ReportTextBox.Text = If(_ShowingToolOutput, _Report.ToolOutput, _Report.ToText())
        ToolOutputButton.Content = If(_ShowingToolOutput, Localizer.T("Show summary"), Localizer.T("Show tool output"))
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        Close()
    End Sub

End Class
