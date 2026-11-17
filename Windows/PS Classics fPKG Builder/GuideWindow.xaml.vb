''' <summary>Step by step guide: what to fill, what is optional, images, emulators, helpers and how to install on the console.</summary>
Public Class GuideWindow

    Private _ScrollToInstall As Boolean

    Public Sub New()
        InitializeComponent()
        UiTranslator.Attach(Me)
    End Sub

    ''' <summary>Opens the guide at the section about installing the package on the console.</summary>
    Public Sub ShowInstallSection()
        _ScrollToInstall = True
    End Sub

    Private Sub GuideWindow_ContentRendered(sender As Object, e As EventArgs) Handles Me.ContentRendered
        If _ScrollToInstall Then InstallSection.BringIntoView(New Rect(0, 0, 1, ActualHeight - 120))
    End Sub

End Class
