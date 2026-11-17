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
            ResultTitle.Text = "No problems found"
            ResultSubtitle.Text = "The package is well formed. Sony certification rules that fake packages never meet are listed below and can be ignored."
        Else
            ResultIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.ErrorCircle24
            ResultIcon.Foreground = New SolidColorBrush(Color.FromRgb(&HFF, &H99, &HA4))
            ResultTitle.Text = Report.ProblemIssues.Count().ToString() + " problem(s) found"
            ResultSubtitle.Text = "These findings may stop the package from installing or running. Details below."
        End If

        ReportTextBox.Text = Report.ToText()
    End Sub

    Private Sub CopyButton_Click(sender As Object, e As RoutedEventArgs) Handles CopyButton.Click
        Clipboard.SetText(If(_ShowingToolOutput, _Report.ToolOutput, _Report.ToText()))
    End Sub

    Private Sub ToolOutputButton_Click(sender As Object, e As RoutedEventArgs) Handles ToolOutputButton.Click
        _ShowingToolOutput = Not _ShowingToolOutput
        ReportTextBox.Text = If(_ShowingToolOutput, _Report.ToolOutput, _Report.ToText())
        ToolOutputButton.Content = If(_ShowingToolOutput, "Show summary", "Show tool output")
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        Close()
    End Sub

End Class
