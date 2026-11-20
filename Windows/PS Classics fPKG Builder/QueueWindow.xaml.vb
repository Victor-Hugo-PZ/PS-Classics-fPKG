Imports System.IO
Imports System.Threading

''' <summary>
''' Builds several games one after the other from .zip or .7z files, with no questions (see <see cref="BatchBuilder"/>).
''' Archives can be dropped on the window or added with the button.
''' </summary>
Public Class QueueWindow

    Private ReadOnly _Queue As New BatchQueue()
    Private _Cancel As CancellationTokenSource
    Private _Running As Boolean

    Public Sub New()
        InitializeComponent()
        'The presets that every platform has
        For Each Preset As PS1Preset In EmulatorPresets.PS1
            PresetComboBox.Items.Add(Localizer.T(Preset.Name))
        Next
        PresetComboBox.SelectedIndex = 0
        'The settings of one game: the first entry gives it back to "Settings for all games"
        GamePresetComboBox.Items.Add(Localizer.T("Same as all games"))
        For Each Name As String In EmulatorPresets.AllNames()
            GamePresetComboBox.Items.Add(Localizer.T(Name))
        Next
        GamePresetComboBox.SelectedIndex = 0
        OutputFolderTextBox.Text = AppSettings.Current.QueueOutputFolder
        RefreshList()
        UiTranslator.Attach(Me)
    End Sub

    ''' <summary>Adds archives to the queue (used by the main window when archives are dropped on it too).</summary>
    Public Sub AddArchives(Files As IEnumerable(Of String))
        _Queue.Add(Files)
        RefreshList()
    End Sub

    Private Sub RefreshList()
        Dim Selected As List(Of Integer) = ItemsListBox.SelectedItems.Cast(Of Object)().Select(Function(Row) ItemsListBox.Items.IndexOf(Row)).ToList()
        ItemsListBox.Items.Clear()
        For Each Item As BatchItem In _Queue.Items
            ItemsListBox.Items.Add(Item.Describe())
        Next
        For Each Index As Integer In Selected
            If Index >= 0 AndAlso Index < ItemsListBox.Items.Count Then ItemsListBox.SelectedItems.Add(ItemsListBox.Items(Index))
        Next
        EmptyText.Visibility = If(_Queue.Items.Count = 0, Visibility.Visible, Visibility.Collapsed)
        SummaryText.Text = If(_Queue.Items.Count = 0, "", _Queue.Summary())
        Dim Idle As Boolean = Not _Running
        StartButton.IsEnabled = Idle AndAlso _Queue.ToBuild().Count > 0
        AddButton.IsEnabled = Idle
        RemoveButton.IsEnabled = Idle
        ClearDoneButton.IsEnabled = Idle
        BrowseOutputButton.IsEnabled = Idle
        PresetComboBox.IsEnabled = Idle
        GamePresetComboBox.IsEnabled = Idle
        ApplyPresetButton.IsEnabled = Idle
        OnlineCheckBox.IsEnabled = Idle
    End Sub

    ''' <summary>The games selected in the list (the list rows are in the same order as the queue).</summary>
    Private Function SelectedGames() As List(Of BatchItem)
        Return ItemsListBox.SelectedItems.Cast(Of Object)().Select(Function(Row) ItemsListBox.Items.IndexOf(Row)).
            Where(Function(Index) Index >= 0 AndAlso Index < _Queue.Items.Count).Select(Function(Index) _Queue.Items(Index)).ToList()
    End Function

    Private Sub ApplyPresetButton_Click(sender As Object, e As RoutedEventArgs) Handles ApplyPresetButton.Click
        Dim Games As List(Of BatchItem) = SelectedGames()
        If Games.Count = 0 Then
            MsgBox(Localizer.T("Select the games in the list first (Ctrl or Shift to select several)."), MsgBoxStyle.Information)
            Return
        End If
        Dim Names As List(Of String) = EmulatorPresets.AllNames()
        Dim Index As Integer = GamePresetComboBox.SelectedIndex
        _Queue.SetPreset(Games, If(Index <= 0 OrElse Index > Names.Count, "", Names(Index - 1)))
        RefreshList()
    End Sub

    Private Sub OpenOutputButton_Click(sender As Object, e As RoutedEventArgs) Handles OpenOutputButton.Click
        Dim Folder As String = OutputFolderTextBox.Text
        If String.IsNullOrWhiteSpace(Folder) OrElse Not Directory.Exists(Folder) Then
            MsgBox(Localizer.T("The output folder does not exist yet."), MsgBoxStyle.Information)
            Return
        End If
        Process.Start(New ProcessStartInfo("explorer", """" + Folder + """") With {.UseShellExecute = True})
    End Sub

    Private Sub AddButton_Click(sender As Object, e As RoutedEventArgs) Handles AddButton.Click
        Dim OFD As New Microsoft.Win32.OpenFileDialog() With {
            .Title = Localizer.T("Select the game archives"), .Filter = "ZIP / 7Z (*.zip;*.7z)|*.zip;*.7z", .Multiselect = True}
        If OFD.ShowDialog() = True Then AddArchives(OFD.FileNames)
    End Sub

    Private Sub RemoveButton_Click(sender As Object, e As RoutedEventArgs) Handles RemoveButton.Click
        Dim Indexes As List(Of Integer) = ItemsListBox.SelectedItems.Cast(Of Object)().Select(Function(Row) ItemsListBox.Items.IndexOf(Row)).OrderByDescending(Function(Index) Index).ToList()
        For Each Index As Integer In Indexes
            If Index >= 0 AndAlso Index < _Queue.Items.Count Then _Queue.Items.RemoveAt(Index)
        Next
        RefreshList()
    End Sub

    Private Sub ClearDoneButton_Click(sender As Object, e As RoutedEventArgs) Handles ClearDoneButton.Click
        _Queue.ClearDone()
        RefreshList()
    End Sub

    Private Sub BrowseOutputButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowseOutputButton.Click
        Dim FBD As New System.Windows.Forms.FolderBrowserDialog() With {.Description = Localizer.T("Please select an output folder"), .ShowNewFolderButton = True, .UseDescriptionForTitle = True}
        If FBD.ShowDialog() = System.Windows.Forms.DialogResult.OK Then OutputFolderTextBox.Text = FBD.SelectedPath
    End Sub

    Private Sub QueueWindow_DragOver(sender As Object, e As DragEventArgs) Handles Me.PreviewDragOver
        e.Effects = If(e.Data.GetDataPresent(DataFormats.FileDrop), DragDropEffects.Copy, DragDropEffects.None)
        e.Handled = True
    End Sub

    Private Sub QueueWindow_Drop(sender As Object, e As DragEventArgs) Handles Me.PreviewDrop
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        e.Handled = True
        AddArchives(CType(e.Data.GetData(DataFormats.FileDrop), String()))
    End Sub

    Private Async Sub StartButton_Click(sender As Object, e As RoutedEventArgs) Handles StartButton.Click
        If String.IsNullOrWhiteSpace(OutputFolderTextBox.Text) Then
            MsgBox(Localizer.T("Choose the output folder first."), MsgBoxStyle.Exclamation)
            Return
        End If
        Try
            Directory.CreateDirectory(OutputFolderTextBox.Text)
        Catch ex As Exception
            MsgBox(Localizer.F("The output folder cannot be used: {0}", ex.Message), MsgBoxStyle.Exclamation)
            Return
        End Try

        _Queue.ResetFailed()
        Dim Options As New BatchOptions() With {
            .OutputFolder = OutputFolderTextBox.Text,
            .PresetName = EmulatorPresets.PS1(Math.Max(0, PresetComboBox.SelectedIndex)).Name,
            .FindCovers = (OnlineCheckBox.IsChecked = True),
            .FindTitles = (OnlineCheckBox.IsChecked = True),
            .Report = Sub(Item) Dispatcher.BeginInvoke(Sub() RefreshList())
        }
        AppSettings.Current.QueueOutputFolder = OutputFolderTextBox.Text
        AppSettings.Current.Save()

        _Running = True
        _Cancel = New CancellationTokenSource()
        Dim Token As CancellationToken = _Cancel.Token
        StartButton.Visibility = Visibility.Collapsed
        CancelButton.Visibility = Visibility.Visible
        CancelButton.IsEnabled = True
        QueueProgress.Visibility = Visibility.Visible
        RefreshList()

        Dim ToBuild As List(Of BatchItem) = _Queue.ToBuild()
        Try
            Await Task.Run(Function() BatchBuilder.BuildAll(ToBuild, Options, Token))
        Finally
            _Running = False
            StartButton.Visibility = Visibility.Visible
            CancelButton.Visibility = Visibility.Collapsed
            QueueProgress.Visibility = Visibility.Collapsed
            RefreshList()
        End Try
    End Sub

    Private Sub CancelButton_Click(sender As Object, e As RoutedEventArgs) Handles CancelButton.Click
        _Cancel?.Cancel()
        CancelButton.IsEnabled = False
    End Sub

    Private Sub CloseButton_Click(sender As Object, e As RoutedEventArgs) Handles CloseButton.Click
        Close()
    End Sub

    Private Sub QueueWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs) Handles Me.Closing
        If _Running Then
            MsgBox(Localizer.T("The queue is still building. Press 'Stop after this game' and wait before closing."), MsgBoxStyle.Information)
            e.Cancel = True
        End If
    End Sub

End Class
