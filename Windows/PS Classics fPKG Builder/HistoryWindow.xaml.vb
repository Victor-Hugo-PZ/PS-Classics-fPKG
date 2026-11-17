''' <summary>List of the packages built on this computer, with the options used and how they run on the console.</summary>
Public Class HistoryWindow

    ''' <summary>Row of the list (text already translated).</summary>
    Public Class HistoryRow
        Public Property Entry As HistoryEntry
        Public Property Title As String
        Public Property Subtitle As String
    End Class

    Private _Entries As List(Of HistoryEntry)

    Public Sub New()
        InitializeComponent()
        UiTranslator.Attach(Me)
        FillConsoleResults()
        Reload(Nothing)
    End Sub

    Private Sub FillConsoleResults()
        ConsoleResultComboBox.Items.Clear()
        For Each Name As String In BuildHistory.ConsoleResultNames
            ConsoleResultComboBox.Items.Add(Localizer.T(Name))
        Next
    End Sub

    Private Sub Reload(SelectId As String)
        _Entries = BuildHistory.Load()
        Dim Rows As List(Of HistoryRow) = _Entries.Select(Function(Entry) New HistoryRow With {
            .Entry = Entry,
            .Title = If(Entry.Title = "", Entry.NPTitle, Entry.Title),
            .Subtitle = Entry.Platform + " · " + Entry.CreatedAt.ToString("g") + " · " +
                        If(Entry.Succeeded, Localizer.T(BuildHistory.ConsoleResultNames(CInt(Entry.ConsoleResult))), Localizer.T("Build failed"))
        }).ToList()
        EntriesListBox.ItemsSource = Rows
        EmptyText.Visibility = If(Rows.Count = 0, Visibility.Visible, Visibility.Collapsed)
        Dim ToSelect As HistoryRow = Rows.FirstOrDefault(Function(Row) Row.Entry.Id = SelectId)
        EntriesListBox.SelectedItem = If(ToSelect, Rows.FirstOrDefault())
    End Sub

    Private ReadOnly Property Selected As HistoryEntry
        Get
            Return TryCast(EntriesListBox.SelectedItem, HistoryRow)?.Entry
        End Get
    End Property

    Private Sub EntriesListBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles EntriesListBox.SelectionChanged
        Dim Entry As HistoryEntry = Selected
        DetailsPanel.IsEnabled = Entry IsNot Nothing
        If Entry Is Nothing Then
            DetailsTitle.Text = ""
            DetailsInfo.Text = ""
            DetailsSettings.Text = ""
            NotesTextBox.Text = ""
            Return
        End If
        DetailsTitle.Text = If(Entry.Title = "", Entry.NPTitle, Entry.Title)
        DetailsInfo.Text = Entry.Platform + " · " + Entry.NPTitle + " · " + Entry.CreatedAt.ToString("g") + vbLf +
                           If(Entry.Succeeded, If(Entry.PackagePath <> "", Entry.PackagePath, Entry.OutputFolder), Localizer.T("Build failed"))
        DetailsSettings.Text = Entry.Settings
        ConsoleResultComboBox.SelectedIndex = CInt(Entry.ConsoleResult)
        NotesTextBox.Text = Entry.Notes
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As RoutedEventArgs) Handles SaveButton.Click
        Dim Entry As HistoryEntry = Selected
        If Entry Is Nothing Then Return
        Entry.ConsoleResult = CType(Math.Max(0, ConsoleResultComboBox.SelectedIndex), ConsoleResult)
        Entry.Notes = NotesTextBox.Text
        BuildHistory.AddOrUpdate(Entry)
        Reload(Entry.Id)
    End Sub

    Private Sub OpenFolderButton_Click(sender As Object, e As RoutedEventArgs) Handles OpenFolderButton.Click
        Dim Entry As HistoryEntry = Selected
        If Entry Is Nothing Then Return
        If Entry.PackagePath <> "" AndAlso IO.File.Exists(Entry.PackagePath) Then
            Process.Start(New ProcessStartInfo("explorer", "/select,""" + Entry.PackagePath + """") With {.UseShellExecute = True})
        ElseIf IO.Directory.Exists(Entry.OutputFolder) Then
            Process.Start(New ProcessStartInfo("explorer", """" + Entry.OutputFolder + """") With {.UseShellExecute = True})
        Else
            MsgBox(Localizer.T("The folder of this package no longer exists."), MsgBoxStyle.Information, Localizer.T("History"))
        End If
    End Sub

    Private Sub DeleteButton_Click(sender As Object, e As RoutedEventArgs) Handles DeleteButton.Click
        Dim Entry As HistoryEntry = Selected
        If Entry Is Nothing Then Return
        If MsgBox(Localizer.T("Remove this entry from the history? The package file is not deleted."), MsgBoxStyle.YesNo, Localizer.T("History")) = MsgBoxResult.Yes Then
            BuildHistory.Remove(Entry.Id)
            Reload(Nothing)
        End If
    End Sub

End Class
