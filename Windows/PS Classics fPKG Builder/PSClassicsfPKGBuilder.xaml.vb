Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' Main window. The handlers only read the controls, build a *BuildRequest and show messages: the logic is in the
''' shared Core library (also used by the Linux/macOS app in CrossPlatform/, which mirrors this window).
''' </summary>
Public Class PSClassicsfPKGBuilder

    Private _Building As Boolean
    Private _FillingPresets As Boolean
    Private _LatestUpdate As UpdateInfo

#Region "Startup, language and settings"

    Private Sub PSClassicsfPKGBuilder_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        'Default images of the emulators: they end up in the package when the user does not pick an icon
        Dim EmulatorsDirectory As String = Path.Combine(AppPaths.ToolsDirectory, "PS4", "emus")
        PS1Preview.FallbackIconPath = Path.Combine(EmulatorsDirectory, "ps1hd", "sce_sys", "icon0.png")
        PS1Preview.FallbackBackgroundPath = Path.Combine(EmulatorsDirectory, "ps1hd", "sce_sys", "pic1.png")
        UpdatePS2PreviewFallbacks()

        SetUpStepBar(PS1StepBar, 0)
        SetUpStepBar(PS2StepBar, 1)
        SetUpStepBar(PSPStepBar, 2)
        FillPresets()
        UiTranslator.Attach(Me)
        AddHandler Localizer.LanguageChanged, AddressOf OnLanguageChanged
        ApplyGuidedMode()
        UpdateToolsStatus(True)
        ShowKnownUpdate()
        CheckForUpdates()
    End Sub

    ''' <summary>Texts set by code follow the language too (the XAML texts are handled by <see cref="UiTranslator"/>).</summary>
    Private Sub OnLanguageChanged(sender As Object, e As EventArgs)
        Dispatcher.Invoke(Sub()
                              FillPresets()
                              UpdateToolsStatus(False)
                              UpdateEmulatorHint()
                              ShowKnownUpdate()
                              For Page As Integer = 0 To 2
                                  ShowStep(Page)
                              Next
                          End Sub)
    End Sub

    Private Sub UpdateToolsStatus(WarnWhenMissing As Boolean)
        If AppPaths.ToolsAvailable() Then
            ToolsStatusText.Text = Localizer.T("Tools folder found")
            ToolsStatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.CheckmarkCircle24
        Else
            ToolsStatusText.Text = Localizer.T("Tools folder missing")
            ToolsStatusIcon.Symbol = Wpf.Ui.Controls.SymbolRegular.Warning24
            ToolsStatusIcon.Foreground = New SolidColorBrush(Color.FromRgb(&HFF, &HC8, &H57))
            If WarnWhenMissing Then
                MsgBox(Localizer.F("The 'Tools' folder was not found at:\n{0}\n\nBuilding fPKGs will fail until it is copied next to the application (see README.md, 'Building from source').", AppPaths.ToolsDirectory),
                       MsgBoxStyle.Exclamation, Localizer.T("Tools folder missing"))
            End If
        End If
    End Sub

    ''' <summary>Shows the page of the platform selected in the navigation list.</summary>
    Private Sub PlatformListBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles PlatformListBox.SelectionChanged
        If PS1Page Is Nothing OrElse PS2Page Is Nothing OrElse PSPPage Is Nothing Then Return
        PS1Page.Visibility = If(PlatformListBox.SelectedIndex = 0, Visibility.Visible, Visibility.Collapsed)
        PS2Page.Visibility = If(PlatformListBox.SelectedIndex = 1, Visibility.Visible, Visibility.Collapsed)
        PSPPage.Visibility = If(PlatformListBox.SelectedIndex = 2, Visibility.Visible, Visibility.Collapsed)
    End Sub

    Private Sub OpenGuideButton_Click(sender As Object, e As RoutedEventArgs) Handles OpenGuideButton.Click
        Dim Guide As New GuideWindow() With {.Owner = Me}
        Guide.ShowDialog()
    End Sub

    Private Sub HistoryButton_Click(sender As Object, e As RoutedEventArgs) Handles HistoryButton.Click
        Dim History As New HistoryWindow() With {.Owner = Me}
        History.ShowDialog()
    End Sub

    Private Sub SettingsButton_Click(sender As Object, e As RoutedEventArgs) Handles SettingsButton.Click
        Dim Settings As New SettingsWindow() With {.Owner = Me}
        Settings.ShowDialog()
        ApplyGuidedMode()
    End Sub

#End Region

#Region "Updates"

    ''' <summary>Shows the banner when a newer version is already known (from an earlier check).</summary>
    Private Sub ShowKnownUpdate()
        Dim Settings As AppSettings = AppSettings.Current
        Dim Known As Version = UpdateChecker.ParseVersion(Settings.LatestVersionTag)
        Dim IsNewer As Boolean = Known IsNot Nothing AndAlso UpdateChecker.IsNewer(Known, UpdateChecker.CurrentVersion())
        If Settings.CheckForUpdates AndAlso IsNewer AndAlso Settings.LatestVersionTag <> Settings.SkippedVersion Then
            UpdateText.Text = Localizer.F("Version {0} is available.", Settings.LatestVersionTag)
            UpdatePanel.Visibility = Visibility.Visible
        Else
            UpdatePanel.Visibility = Visibility.Collapsed
        End If
    End Sub

    Private Async Sub CheckForUpdates()
        Dim Settings As AppSettings = AppSettings.Current
        If Not UpdateChecker.IsCheckDue(Settings, DateTime.Now) Then Return
        Dim Info As UpdateInfo = Await UpdateChecker.CheckAsync()
        If Info Is Nothing Then Return
        Settings.LastUpdateCheck = DateTime.Now
        Settings.LatestVersionTag = Info.Tag
        Settings.LatestVersionUrl = Info.Url
        Settings.Save()
        ShowKnownUpdate()
    End Sub

    Private Sub DownloadUpdateButton_Click(sender As Object, e As RoutedEventArgs) Handles DownloadUpdateButton.Click
        Dim Url As String = If(String.IsNullOrEmpty(AppSettings.Current.LatestVersionUrl), UpdateChecker.ReleasesPage, AppSettings.Current.LatestVersionUrl)
        Process.Start(New ProcessStartInfo(Url) With {.UseShellExecute = True})
    End Sub

    Private Sub SkipUpdateButton_Click(sender As Object, e As RoutedEventArgs) Handles SkipUpdateButton.Click
        AppSettings.Current.SkippedVersion = AppSettings.Current.LatestVersionTag
        AppSettings.Current.Save()
        UpdatePanel.Visibility = Visibility.Collapsed
    End Sub

#End Region

#Region "Step-by-step mode"

    Private Function StepBarOf(Page As Integer) As StepBar
        Return {PS1StepBar, PS2StepBar, PSPStepBar}(Page)
    End Function

    ''' <summary>Sections shown in each step of a page (steps 0 to 2; step 3 is the review card).</summary>
    Private Function StepSections(Page As Integer) As FrameworkElement()()
        Select Case Page
            Case 0
                Return {New FrameworkElement() {PS1GameSection}, New FrameworkElement() {PS1ImagesSection}, New FrameworkElement() {PS1DiscsSection, PS1EmulatorSection, PS1AdvancedSection}}
            Case 1
                Return {New FrameworkElement() {PS2GameSection}, New FrameworkElement() {PS2ImagesSection}, New FrameworkElement() {PS2DiscsSection, PS2EmulatorSection, PS2ConfigsSection}}
            Case Else
                Return {New FrameworkElement() {PSPGameSection}, New FrameworkElement() {PSPImagesSection}, New FrameworkElement() {PSPEmulatorSection}}
        End Select
    End Function

    Private Sub SetUpStepBar(Bar As StepBar, Page As Integer)
        Bar.CanLeaveStep = Function(CurrentStep) CanLeaveStep(Page, CurrentStep)
        AddHandler Bar.StepChanged, Sub(sender, e) ShowStep(Page)
    End Sub

    Private Sub ApplyGuidedMode()
        Dim Guided As Boolean = AppSettings.Current.GuidedMode
        For Page As Integer = 0 To 2
            StepBarOf(Page).Visibility = If(Guided, Visibility.Visible, Visibility.Collapsed)
            ShowStep(Page)
        Next
    End Sub

    Private Sub ShowStep(Page As Integer)
        Dim Guided As Boolean = AppSettings.Current.GuidedMode
        Dim CurrentStep As Integer = StepBarOf(Page).CurrentStep
        Dim Steps As FrameworkElement()() = StepSections(Page)
        For StepIndex As Integer = 0 To Steps.Length - 1
            For Each Section As FrameworkElement In Steps(StepIndex)
                Section.Visibility = If(Not Guided OrElse StepIndex = CurrentStep, Visibility.Visible, Visibility.Collapsed)
                If Guided AndAlso StepIndex = CurrentStep AndAlso TypeOf Section Is Expander Then CType(Section, Expander).IsExpanded = True
            Next
        Next
        Dim ReviewCard As FrameworkElement = {PS1ReviewCard, PS2ReviewCard, PSPReviewCard}(Page)
        ReviewCard.Visibility = If(Guided AndAlso CurrentStep = StepBar.StepCount - 1, Visibility.Visible, Visibility.Collapsed)
        If ReviewCard.Visibility = Visibility.Visible Then UpdateReview(Page)
    End Sub

    ''' <summary>Step 1 needs the game, a title and a valid NP title before going on.</summary>
    Private Function CanLeaveStep(Page As Integer, CurrentStep As Integer) As Boolean
        If CurrentStep <> 0 Then Return True
        Dim Disc As String = {PS1SelectedDisc1TextBox.Text, SelectedDisc1TextBox.Text, SelectedPSPDiscTextBox.Text}(Page)
        Dim Title As String = {PS1TitleTextBox.Text, PS2TitleTextBox.Text, PSPTitleTextBox.Text}(Page)
        Dim NPTitle As String = {PS1NPTitleTextBox.Text, PS2NPTitleTextBox.Text, PSPNPTitleTextBox.Text}(Page)
        Dim Problems As New List(Of PreflightIssue)
        If String.IsNullOrWhiteSpace(Disc) Then Problems.Add(New PreflightIssue With {.IsError = True, .Message = Localizer.T("Select the game file first."), .Suggestion = Localizer.T("Press Browse… next to Disc 1, or drag the game onto the window.")})
        If String.IsNullOrWhiteSpace(Title) Then Problems.Add(New PreflightIssue With {.IsError = True, .Message = Localizer.T("No game title specified."), .Suggestion = Localizer.T("Type the name that the home screen will show.")})
        Problems.AddRange(BuildPreflight.CheckNPTitle(NPTitle).Where(Function(Issue) Issue.IsError))
        If Problems.Count = 0 Then Return True
        MsgBox(BuildPreflight.Describe(Problems), MsgBoxStyle.Exclamation, Localizer.T("Before the next step"))
        Return False
    End Function

    Private Sub UpdateReview(Page As Integer)
        Dim Lines As New List(Of String)
        Dim Describe = Function(Value As String, Fallback As String) If(String.IsNullOrWhiteSpace(Value), Fallback, Path.GetFileName(Value))
        Select Case Page
            Case 0
                Dim Request As PS1BuildRequest = CreatePS1Request()
                Lines.AddRange(ReviewHeader(Request.Title, Request.NPTitle, {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4}))
                Lines.Add(Localizer.T("Icon") + ": " + Describe(Request.IconFile, Localizer.T("default of the emulator")))
                Lines.Add(Localizer.T("Background") + ": " + Describe(Request.BackgroundFile, Localizer.T("default of the emulator")))
                Lines.Add("")
                Lines.Add(BuildHistory.Summarize(Request))
                PS1ReviewText.Text = String.Join(vbLf, Lines)
            Case 1
                Dim Request As PS2BuildRequest = CreatePS2Request()
                Lines.AddRange(ReviewHeader(Request.Title, Request.NPTitle, {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4, Request.Disc5}))
                Lines.Add(Localizer.T("Icon") + ": " + Describe(Request.IconFile, Localizer.T("default of the emulator")))
                Lines.Add(Localizer.T("Background") + ": " + Describe(Request.BackgroundFile, Localizer.T("default of the emulator")))
                Lines.Add("")
                Lines.Add(BuildHistory.Summarize(Request))
                PS2ReviewText.Text = String.Join(vbLf, Lines)
            Case Else
                Dim Request As PSPBuildRequest = CreatePSPRequest()
                Lines.AddRange(ReviewHeader(Request.Title, Request.NPTitle, {Request.IsoFile}))
                Lines.Add(Localizer.T("Icon") + ": " + Describe(Request.IconFile, Localizer.T("none (empty tile)")))
                Lines.Add(Localizer.T("Background") + ": " + Describe(Request.BackgroundFile, Localizer.T("none")))
                Lines.Add("")
                Lines.Add(BuildHistory.Summarize(Request))
                PSPReviewText.Text = String.Join(vbLf, Lines)
        End Select
    End Sub

    Private Shared Function ReviewHeader(Title As String, NPTitle As String, Discs As String()) As List(Of String)
        Dim Lines As New List(Of String) From {Localizer.T("Title") + ": " + Title, "NP Title: " + NPTitle}
        Dim Selected As String() = Discs.Where(Function(Disc) Not String.IsNullOrWhiteSpace(Disc)).Select(Function(Disc) Path.GetFileName(Disc)).ToArray()
        Lines.Add(Localizer.T("Discs") + ": " + If(Selected.Length = 0, "-", String.Join(", ", Selected)))
        Return Lines
    End Function

#End Region

#Region "Presets"

    Private Sub FillPresets()
        _FillingPresets = True
        Dim PS1Selected As Integer = PS1PresetComboBox.SelectedIndex
        Dim PS2Selected As Integer = PS2PresetComboBox.SelectedIndex
        PS1PresetComboBox.Items.Clear()
        For Each Preset As PS1Preset In EmulatorPresets.PS1
            PS1PresetComboBox.Items.Add(Localizer.T(Preset.Name))
        Next
        PS2PresetComboBox.Items.Clear()
        For Each Preset As PS2Preset In EmulatorPresets.PS2
            PS2PresetComboBox.Items.Add(Localizer.T(Preset.Name))
        Next
        PS1PresetComboBox.SelectedIndex = PS1Selected
        PS2PresetComboBox.SelectedIndex = PS2Selected
        If PS1Selected >= 0 Then PS1PresetHint.Text = PresetHint(EmulatorPresets.PS1(PS1Selected).Description)
        If PS2Selected >= 0 Then PS2PresetHint.Text = PresetHint(EmulatorPresets.PS2(PS2Selected).Description)
        _FillingPresets = False
    End Sub

    Private Shared Function PresetHint(Description As String) As String
        Return Localizer.T(Description) + " " + Localizer.T("You can still change any option below.")
    End Function

    Private Shared Sub SelectItem(Combo As ComboBox, Value As String)
        For Index As Integer = 0 To Combo.Items.Count - 1
            If CStr(TryCast(Combo.Items(Index), ComboBoxItem)?.Content) = Value Then
                Combo.SelectedIndex = Index
                Return
            End If
        Next
    End Sub

    Private Sub PS1PresetComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles PS1PresetComboBox.SelectionChanged
        If _FillingPresets OrElse PS1PresetComboBox.SelectedIndex < 0 Then Return
        Dim Preset As PS1Preset = EmulatorPresets.PS1(PS1PresetComboBox.SelectedIndex)
        SelectItem(PS1UpscalingComboBox, Preset.Upscaling)
        PS1SkipBootlogoCheckBox.IsChecked = Preset.SkipBootlogo
        PS1EmulateAnalogSticksCheckBox.IsChecked = Preset.EmulateAnalogSticks
        PS1Force60HzCheckBox.IsChecked = Preset.Force60Hz
        PS1PresetHint.Text = PresetHint(Preset.Description)
    End Sub

    Private Sub PS2PresetComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles PS2PresetComboBox.SelectionChanged
        If _FillingPresets OrElse PS2PresetComboBox.SelectedIndex < 0 Then Return
        Dim Preset As PS2Preset = EmulatorPresets.PS2(PS2PresetComboBox.SelectedIndex)
        SelectItem(PS2EmulatorComboBox, Preset.Emulator)
        SelectItem(PS2UpscalingComboBox, Preset.Upscaling)
        SelectItem(PS2UprenderComboBox, Preset.Uprender)
        SelectItem(PS2DisplayModeComboBox, Preset.DisplayMode)
        PS2FixGraphicsCheckBox.IsChecked = Preset.FixGraphics
        PS2ImproveSpeedCheckBox.IsChecked = Preset.ImproveSpeed
        PS2DisableMTVUCheckBox.IsChecked = Preset.DisableMTVU
        PS2DisableInstantVIF1TransferCheckBox.IsChecked = Preset.DisableInstantVIF1Transfer
        If PS2UseWidescreenPatchCheckBox.IsEnabled Then PS2UseWidescreenPatchCheckBox.IsChecked = Preset.WidescreenPatchWhenAvailable
        PS2UseCompressionCheckBox.IsChecked = Preset.UseCompression
        PS2PresetHint.Text = PresetHint(Preset.Description)
    End Sub

#End Region

#Region "Shared helpers"

    Private Sub PS2EmulatorComboBox_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles PS2EmulatorComboBox.SelectionChanged
        UpdatePS2PreviewFallbacks()
        UpdateEmulatorHint()
    End Sub

    ''' <summary>Hint under the emulator field: describes the choice that is selected now.</summary>
    Private Sub UpdateEmulatorHint()
        If PS2EmulatorHint Is Nothing OrElse PS2EmulatorComboBox Is Nothing Then Return
        PS2EmulatorHint.Text = If(CStr(TryCast(PS2EmulatorComboBox.SelectedItem, ComboBoxItem)?.Content) = "Rogue v1",
                                  Localizer.T("Rogue v1 selected. If the game does not start or crashes, switch back to Jak v2 (the default)."),
                                  Localizer.T("Default: Jak v2. If the game does not start or crashes, try Rogue v1."))
    End Sub

    ''' <summary>The default icon and background of a PS2 package come from the selected emulator.</summary>
    Private Sub UpdatePS2PreviewFallbacks()
        If PS2Preview Is Nothing OrElse PS2EmulatorComboBox Is Nothing Then Return
        Dim SelectedItem As ComboBoxItem = TryCast(PS2EmulatorComboBox.SelectedItem, ComboBoxItem)
        If SelectedItem Is Nothing Then Return
        Dim SceSys As String = Path.Combine(AppPaths.ToolsDirectory, "PS4", "emus", CStr(SelectedItem.Content), "sce_sys")
        PS2Preview.FallbackIconPath = Path.Combine(SceSys, "icon0.png")
        PS2Preview.FallbackBackgroundPath = Path.Combine(SceSys, "pic1.png")
    End Sub

    Private Shared Function PickFile(Title As String, Filter As String) As String
        Dim OFD As New System.Windows.Forms.OpenFileDialog() With {.Title = Localizer.T(Title), .Multiselect = False, .Filter = Filter}
        Return If(OFD.ShowDialog() = System.Windows.Forms.DialogResult.OK, OFD.FileName, Nothing)
    End Function

    Private Shared Function PickImage(IsIcon As Boolean) As String
        Return PickFile(If(IsIcon, "Select an image for the icon (PNG, JPG, BMP, GIF or WebP).", "Select an image for the background (PNG, JPG, BMP, GIF or WebP)."), ImageFormats.OpenFilter())
    End Function

    Private Sub VerifyPkgButton_Click(sender As Object, e As RoutedEventArgs) Handles VerifyPkgButton.Click
        Dim PkgFile As String = PickFile("Select the .pkg to check.", "PS4 package (*.pkg)|*.pkg")
        If PkgFile IsNot Nothing Then CheckPackage(PkgFile)
    End Sub

    Private Async Sub CheckPackage(PkgFile As String)
        VerifyPkgButton.IsEnabled = False
        VerifyPkgButton.Content = Localizer.T("Checking…")
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait
        Try
            Dim Report As PackageReport = Await Task.Run(Function() PackageVerifier.Verify(PkgFile))
            Mouse.OverrideCursor = Nothing
            Dim ReportWindow As New PackageReportWindow(Report) With {.Owner = Me}
            ReportWindow.ShowDialog()
        Catch ex As Exception
            Mouse.OverrideCursor = Nothing
            MsgBox(Localizer.F("The package could not be checked:\n{0}", ex.Message), MsgBoxStyle.Critical, Localizer.T("Check a .pkg"))
        Finally
            Mouse.OverrideCursor = Nothing
            VerifyPkgButton.Content = Localizer.T("Check a .pkg")
            VerifyPkgButton.IsEnabled = True
        End Try
    End Sub

    ''' <summary>
    ''' Common build flow: checks before building (with fixes), output folder, build in the background, automatic package
    ''' check, history entry and a result window that explains failures in plain words.
    ''' </summary>
    Private Async Function RunBuild(PlatformName As String, Title As String, NPTitle As String, Summary As String,
                                    Preflight As Func(Of String, List(Of PreflightIssue)), Build As Func(Of String, PackageBuildResult),
                                    BuildButton As Wpf.Ui.Controls.Button, Progress As ProgressBar) As Task
        If _Building Then Return

        'Problems that do not depend on the output folder are shown before asking for it
        Dim Issues As List(Of PreflightIssue) = Preflight(Nothing)
        If Not ConfirmPreflight(Issues) Then Return

        Dim FBD As New System.Windows.Forms.FolderBrowserDialog() With {.Description = Localizer.T("Please select an output folder"), .ShowNewFolderButton = True, .UseDescriptionForTitle = True}
        If FBD.ShowDialog() <> System.Windows.Forms.DialogResult.OK Then Return
        Dim OutputFolder As String = FBD.SelectedPath

        'Free space on the chosen drive
        Dim SpaceIssues As List(Of PreflightIssue) = Preflight(OutputFolder).Where(Function(Issue) Not Issues.Any(Function(Known) Known.Message = Issue.Message)).ToList()
        If SpaceIssues.Any(Function(Issue) Issue.IsError) Then
            MsgBox(BuildPreflight.Describe(SpaceIssues), MsgBoxStyle.Critical, Localizer.T("Cannot create fPKG"))
            Return
        End If

        _Building = True
        BuildButton.IsEnabled = False
        BuildButton.Content = Localizer.T("Building… (this can take a few minutes)")
        Progress.Visibility = Visibility.Visible

        Dim Result As PackageBuildResult = Nothing
        Dim ErrorDetails As String = ""
        Dim Report As PackageReport = Nothing
        Try
            Result = Await Task.Run(Function() Build(OutputFolder))
            If Not Result.Success Then ErrorDetails = Result.Output
            If Result.Success AndAlso AppSettings.Current.CheckPackageAfterBuild AndAlso Result.PackagePath <> "" Then
                BuildButton.Content = Localizer.T("Checking the package…")
                Dim PkgFile As String = Result.PackagePath
                Report = Await Task.Run(Function() PackageVerifier.Verify(PkgFile, False))
            End If
        Catch ex As Exception
            ErrorDetails = ex.Message
        Finally
            _Building = False
            BuildButton.IsEnabled = True
            BuildButton.Content = Localizer.T("Build fPKG")
            Progress.Visibility = Visibility.Collapsed
        End Try

        BuildHistory.AddOrUpdate(New HistoryEntry With {
            .Platform = PlatformName, .Title = Title, .NPTitle = NPTitle, .Settings = Summary, .OutputFolder = OutputFolder,
            .PackagePath = If(Result?.PackagePath, ""), .Succeeded = Result IsNot Nothing AndAlso Result.Success
        })

        Dim ResultWindow As New BuildResultWindow(Result, OutputFolder, Report, ErrorDetails) With {.Owner = Me}
        ResultWindow.ShowDialog()
    End Function

    ''' <summary>Shows the problems found before a build. Returns False when the build must not start.</summary>
    Private Shared Function ConfirmPreflight(Issues As List(Of PreflightIssue)) As Boolean
        If Issues.Count = 0 Then Return True
        If Issues.Any(Function(Issue) Issue.IsError) Then
            MsgBox(BuildPreflight.Describe(Issues), MsgBoxStyle.Critical, Localizer.T("Cannot create fPKG"))
            Return False
        End If
        Return MsgBox(BuildPreflight.Describe(Issues) + vbLf + vbLf + Localizer.T("Continue anyway?"), MsgBoxStyle.YesNo Or MsgBoxStyle.Exclamation, Localizer.T("Before building")) = MsgBoxResult.Yes
    End Function

    ''' <summary>Asks a yes/no question from the build thread.</summary>
    Private Function AskFromBuild(Message As String) As Boolean
        Return Dispatcher.Invoke(Function() MsgBox(Localizer.T(Message), MsgBoxStyle.YesNo) = MsgBoxResult.Yes)
    End Function

    Private Sub WarnFromBuild(Message As String)
        Dispatcher.Invoke(Sub() MsgBox(Localizer.T(Message), MsgBoxStyle.Exclamation))
    End Sub

    ''' <summary>
    ''' Finds the cover (PS1/PS2) through <see cref="CoverSources"/> (ID collection, then the Redump name, then the title)
    ''' and fills the icon, and the background when it is empty or the user agrees.
    ''' </summary>
    Private Async Function FindCover(PlatformName As String, GameIdOrNPTitle As String, TitleText As String, IconBox As Wpf.Ui.Controls.TextBox, BackgroundBox As Wpf.Ui.Controls.TextBox, Button As Wpf.Ui.Controls.Button) As Task
        If CoverArt.NormalizeSerial(GameIdOrNPTitle) = "" AndAlso String.IsNullOrWhiteSpace(TitleText) Then
            MsgBox(Localizer.T("Select the game first: the game ID (or the NP title) is needed to find its cover."), MsgBoxStyle.Information, Localizer.T("Find cover"))
            Return
        End If
        Dim OriginalContent As Object = Button.Content
        Button.IsEnabled = False
        Button.Content = Localizer.T("Searching…")
        Try
            Dim Found As CoverFound = Await CoverSources.FindCoverAsync(PlatformName, GameIdOrNPTitle, TitleText)
            If Found Is Nothing Then
                MsgBox(Localizer.F("No cover was found for {0}. Choose an image yourself.", CoverArt.NormalizeSerial(GameIdOrNPTitle) + " " + TitleText), MsgBoxStyle.Information, Localizer.T("Find cover"))
                Return
            End If
            If Found.Source <> CoverSource.IdCollection Then
                MsgBox(Localizer.T("The cover was found by the game name, not by its ID. Check the icon and the background in the preview before building."), MsgBoxStyle.Information, Localizer.T("Find cover"))
            End If
            Dim Images As CoverArt.GeneratedImages = Await Task.Run(Function() CoverArt.CreateImages(Found.Image, CoverArt.NormalizeSerial(GameIdOrNPTitle) + "-" + Found.Source.ToString()))
            ApplyImages(Images, IconBox, BackgroundBox)
        Catch ex As Exception
            MsgBox(Localizer.F("The cover could not be downloaded: {0}", ex.Message), MsgBoxStyle.Exclamation, Localizer.T("Find cover"))
        Finally
            Button.Content = Localizer.T(CStr(OriginalContent))
            Button.IsEnabled = True
        End Try
    End Function

    Private Shared Sub ApplyImages(Images As CoverArt.GeneratedImages, IconBox As Wpf.Ui.Controls.TextBox, BackgroundBox As Wpf.Ui.Controls.TextBox)
        IconBox.Text = Images.IconFile
        If String.IsNullOrWhiteSpace(BackgroundBox.Text) OrElse
           MsgBox(Localizer.T("Also replace the background with the one made from these images?"), MsgBoxStyle.YesNo, Localizer.T("Background")) = MsgBoxResult.Yes Then
            BackgroundBox.Text = Images.BackgroundFile
        End If
    End Sub

#End Region

#Region "Drag and drop"

    Private Sub Window_PreviewDragOver(sender As Object, e As System.Windows.DragEventArgs) Handles Me.PreviewDragOver
        e.Effects = If(e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop), System.Windows.DragDropEffects.Copy, System.Windows.DragDropEffects.None)
        e.Handled = True
    End Sub

    Private Async Sub Window_PreviewDrop(sender As Object, e As System.Windows.DragEventArgs) Handles Me.PreviewDrop
        If Not e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) Then Return
        e.Handled = True
        For Each DroppedFile As String In CType(e.Data.GetData(System.Windows.DataFormats.FileDrop), String())
            If ArchiveImport.IsArchive(DroppedFile) Then
                Await HandleDroppedArchive(DroppedFile)
            Else
                Await HandleDroppedFile(DroppedFile)
            End If
        Next
    End Sub

    ''' <summary>Extracts a dropped .zip or .7z and handles the game files in it as if they were dropped one by one.</summary>
    Private Async Function HandleDroppedArchive(ArchiveFile As String) As Task
        Dim Found As List(Of ImportedFile)
        Try
            'Look inside first: an incomplete or protected archive is reported before any file is extracted
            Dim Report As ArchiveReport = Await Task.Run(Function() ArchiveInspector.Inspect(ArchiveFile))
            If Not Report.IsOk Then
                MsgBox(Report.Describe(), MsgBoxStyle.Exclamation)
                Return
            End If
            Found = Await Task.Run(Function()
                                       Dim Folder As String = ArchiveImport.Extract(ArchiveFile)
                                       Return ArchiveImport.FindGameFiles(Folder)
                                   End Function)
        Catch ex As Exception
            MsgBox(Localizer.F("The archive could not be opened: {0}", ex.Message), MsgBoxStyle.Exclamation)
            Return
        End Try
        If Found.Count = 0 Then
            MsgBox(Localizer.T("No game files were found in this archive. A PS1 game needs its .bin and .cue files, a PS2 or PSP game its .iso file."), MsgBoxStyle.Information)
            Return
        End If
        For Each Item As ImportedFile In Found
            Await HandleDroppedFile(Item.Path, Item.Kind)
        Next
    End Function

    ''' <summary>Puts a dropped file in the right field (switching to the right platform for games).</summary>
    Private Async Function HandleDroppedFile(DroppedFile As String, Optional KnownKind As DroppedFileKind? = Nothing) As Task
        Dim Page As Integer = PlatformListBox.SelectedIndex
        Select Case If(KnownKind, DroppedFiles.Classify(DroppedFile))
            Case DroppedFileKind.PS1Disc
                Dim Bin As String = DroppedFiles.DiscFile(DroppedFile)
                If Bin = "" Then
                    MsgBox(Localizer.T("The .BIN file of this .CUE was not found next to it."), MsgBoxStyle.Exclamation)
                    Return
                End If
                PlatformListBox.SelectedIndex = 0
                LoadPS1Disc1(Bin)
            Case DroppedFileKind.PS2Disc
                PlatformListBox.SelectedIndex = 1
                Await LoadPS2Disc1(DroppedFile)
            Case DroppedFileKind.PSPDisc
                PlatformListBox.SelectedIndex = 2
                Await LoadPSPDisc(DroppedFile)
            Case DroppedFileKind.Icon
                SetText(Page, DroppedFile, SelectedPS1IconTextBox, SelectedIconTextBox, SelectedPSPIconTextBox)
            Case DroppedFileKind.Background
                SetText(Page, DroppedFile, SelectedPS1BGTextBox, SelectedStartupImageTextBox, SelectedPSPBGImageTextBox)
            Case DroppedFileKind.TxtConfig
                SetText(Page, DroppedFile, SelectedPS1TXTConfigTextBox, SelectedPS2TXTConfigTextBox, SelectedPSPConfigTextBox)
            Case DroppedFileKind.LuaConfig
                If Page = 2 Then
                    MsgBox(Localizer.T("PSP packages do not use LUA configs."), MsgBoxStyle.Information)
                Else
                    SetText(Page, DroppedFile, SelectedPS1LUAConfigTextBox, SelectedPS2LUAConfigTextBox)
                End If
            Case DroppedFileKind.MemoryCard
                PlatformListBox.SelectedIndex = 1
                SelectedPS2MemoryCardTextBox.Text = DroppedFile
            Case DroppedFileKind.Package
                CheckPackage(DroppedFile)
            Case Else
                MsgBox(Localizer.F("This file type is not supported: {0}", Path.GetFileName(DroppedFile)), MsgBoxStyle.Information)
        End Select
    End Function

    ''' <summary>Fills the text box of the current page (PS1, PS2, PSP order).</summary>
    Private Shared Sub SetText(Page As Integer, Value As String, ParamArray Boxes As Wpf.Ui.Controls.TextBox())
        If Page >= 0 AndAlso Page < Boxes.Length Then Boxes(Page).Text = Value
    End Sub

#End Region

#Region "PS1"

    Dim CurrentPS1GameID As String = ""

    Private Sub BrowsePS1Disc1Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc1Button.Click
        Dim BinFile As String = PickFile("Select a PS1 BIN file.", "BIN (*.bin)|*.bin")
        If BinFile IsNot Nothing Then LoadPS1Disc1(BinFile)
    End Sub

    Private Sub LoadPS1Disc1(BinFile As String)
        'Get the game ID from the BIN file and a title from the master list
        Dim GameID As String = PS1Game.GetPS1GameID(BinFile)
        Dim GameTitle As String = If(GameID.Length = 10, PS1Game.GetPS1GameTitleFromDatabaseList(GameID), "")

        'Forget the ID of a previously selected game (it must not leak into the libcrypt check of this one)
        CurrentPS1GameID = GameID.Trim()

        If Not String.IsNullOrEmpty(GameID) Then
            PS1NPTitleTextBox.Text = GameID.Replace("-"c, "").Trim()
            PS1SelectedDisc1TextBox.Text = BinFile
        ElseIf MsgBox(Localizer.T("Cannot find the game ID within the PS1 BIN file, do you want to use this file anyway ?\nDo not forget to specify a NP title and title if you do so."), MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
            PS1SelectedDisc1TextBox.Text = BinFile
        End If
        If Not String.IsNullOrEmpty(GameTitle) Then
            PS1TitleTextBox.Text = GameTitle
        ElseIf GameID <> "" Then
            FillTitleFromRedump("PS1", GameID, PS1TitleTextBox)
        End If
    End Sub

    ''' <summary>For a game that is not in the title list: asks the Redump database for the title, when the title box is still empty.</summary>
    Private Async Sub FillTitleFromRedump(PlatformName As String, GameID As String, TitleBox As Wpf.Ui.Controls.TextBox)
        If Not String.IsNullOrWhiteSpace(TitleBox.Text) Then Return
        Dim Title As String = Await CoverSources.FindTitleAsync(PlatformName, GameID)
        If Title <> "" AndAlso String.IsNullOrWhiteSpace(TitleBox.Text) Then TitleBox.Text = Title
    End Sub

    Private Sub BrowsePS1IconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1IconButton.Click
        Dim Image As String = PickImage(True)
        If Image IsNot Nothing Then SelectedPS1IconTextBox.Text = Image
    End Sub

    Private Sub BrowsePS1StartupImageButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1StartupImageButton.Click
        Dim Image As String = PickImage(False)
        If Image IsNot Nothing Then SelectedPS1BGTextBox.Text = Image
    End Sub

    Private Async Sub PS1FindCoverButton_Click(sender As Object, e As RoutedEventArgs) Handles PS1FindCoverButton.Click
        Await FindCover("PS1", If(CurrentPS1GameID <> "", CurrentPS1GameID, PS1NPTitleTextBox.Text), PS1TitleTextBox.Text, SelectedPS1IconTextBox, SelectedPS1BGTextBox, PS1FindCoverButton)
    End Sub

    Private Sub BrowsePS1Disc2Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc2Button.Click
        Dim BinFile As String = PickFile("Select a PS1 BIN file.", "BIN (*.bin)|*.bin")
        If BinFile IsNot Nothing Then PS1SelectedDisc2TextBox.Text = BinFile
    End Sub

    Private Sub BrowsePS1Disc3Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc3Button.Click
        Dim BinFile As String = PickFile("Select a PS1 BIN file.", "BIN (*.bin)|*.bin")
        If BinFile IsNot Nothing Then PS1SelectedDisc3TextBox.Text = BinFile
    End Sub

    Private Sub BrowsePS1Disc4Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1Disc4Button.Click
        Dim BinFile As String = PickFile("Select a PS1 BIN file.", "BIN (*.bin)|*.bin")
        If BinFile IsNot Nothing Then PS1SelectedDisc4TextBox.Text = BinFile
    End Sub

    Private Sub BrowsePS1TXTConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1TXTConfigButton.Click
        Dim ConfigFile As String = PickFile("Select a TXT config file.", "TXT (*.txt)|*.txt")
        If ConfigFile IsNot Nothing Then SelectedPS1TXTConfigTextBox.Text = ConfigFile
    End Sub

    Private Sub BrowsePS1LUAConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS1LUAConfigButton.Click
        Dim ConfigFile As String = PickFile("Select a LUA config file.", "LUA (*.lua)|*.lua")
        If ConfigFile IsNot Nothing Then SelectedPS1LUAConfigTextBox.Text = ConfigFile
    End Sub

    Private Function CreatePS1Request() As PS1BuildRequest
        Return New PS1BuildRequest() With {
            .Title = PS1TitleTextBox.Text,
            .NPTitle = PS1NPTitleTextBox.Text,
            .DetectedGameID = CurrentPS1GameID,
            .Disc1 = PS1SelectedDisc1TextBox.Text,
            .Disc2 = PS1SelectedDisc2TextBox.Text,
            .Disc3 = PS1SelectedDisc3TextBox.Text,
            .Disc4 = PS1SelectedDisc4TextBox.Text,
            .IconFile = SelectedPS1IconTextBox.Text,
            .BackgroundFile = SelectedPS1BGTextBox.Text,
            .TxtConfigFile = SelectedPS1TXTConfigTextBox.Text,
            .LuaConfigFile = SelectedPS1LUAConfigTextBox.Text,
            .Upscaling = PS1UpscalingComboBox.Text,
            .SkipBootlogo = (PS1SkipBootlogoCheckBox.IsChecked = True),
            .Guncon = (PS1GunconCheckBox.IsChecked = True),
            .Force60Hz = (PS1Force60HzCheckBox.IsChecked = True),
            .EmulateAnalogSticks = (PS1EmulateAnalogSticksCheckBox.IsChecked = True),
            .ConfirmLibcrypt = Function() AskFromBuild("This game is Libcrypt protected. Do you want to add --libcrypt to the configuration file ?")
        }
    End Function

    Private Async Sub BuildPS1fPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPS1fPKGButton.Click
        Dim Request As PS1BuildRequest = CreatePS1Request()
        Await RunBuild("PS1", Request.Title, Request.NPTitle, BuildHistory.Summarize(Request),
                       Function(Folder) BuildPreflight.CheckPS1(Request, Folder),
                       Function(Folder) PS1PackageBuilder.Build(Request, Folder),
                       BuildPS1fPKGButton, PS1BuildProgress)
    End Sub

#End Region

#Region "PS2"

    Dim CurrentPS2GameID As String = ""
    Dim CurrentPS2GameCRC As String = ""

    Private Async Sub BrowsePS2Disc1Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc1Button.Click
        Dim IsoFile As String = PickFile("Select a PS2 ISO file.", "ISO (*.iso)|*.iso")
        If IsoFile IsNot Nothing Then Await LoadPS2Disc1(IsoFile)
    End Sub

    Private Async Function LoadPS2Disc1(IsoFile As String) As Task
        BrowsePS2Disc1Button.IsEnabled = False
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait
        Dim Detected As (GameID As String, GameCRC As String)
        Try
            Detected = Await Task.Run(Function()
                                          Dim GameID As String = PS2Game.GetPS2GameID(IsoFile)
                                          Return (GameID, IsoTools.GetGameCRC(IsoTools.GetELFfromISO(IsoFile, GameID)))
                                      End Function)
        Catch ex As Exception
            Detected = ("", "")
        Finally
            Mouse.OverrideCursor = Nothing
            BrowsePS2Disc1Button.IsEnabled = True
        End Try
        Dim PS2GameID As String = If(Detected.GameID, "")
        Dim PS2GameCRC As String = If(Detected.GameCRC, "")
        CurrentPS2GameID = PS2GameID
        CurrentPS2GameCRC = PS2GameCRC

        If Not String.IsNullOrEmpty(PS2GameID) AndAlso Not String.IsNullOrEmpty(PS2GameCRC) Then
            Dim ConfigsDirectory As String = Path.Combine(AppPaths.ToolsDirectory, "PS4", "ps2-configs")
            ShowDatabaseOption(PS2AddTXTConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(PS2GameID, Path.Combine(ConfigsDirectory, "configs_txt.dat")))
            ShowDatabaseOption(PS2AddLUAConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(PS2GameID, Path.Combine(ConfigsDirectory, "configs_lua.dat")))
            ShowDatabaseOption(PS2AddPS3ConfigFromDatabaseCheckBox, IsoTools.IsConfigAvailable(PS2GameID + ".CONFIG", Path.Combine(ConfigsDirectory, "configs_ps3.dat")))

            If IsoTools.IsConfigAvailable(PS2GameCRC + ".lua", Path.Combine(ConfigsDirectory, "widescreen.dat")) Then
                PS2UseWidescreenPatchCheckBox.IsChecked = True
                PS2UseWidescreenPatchCheckBox.IsEnabled = True
            Else
                PS2UseWidescreenPatchCheckBox.IsEnabled = False
            End If

            PS2TitleTextBox.Text = PS2Game.GetPS2GameTitleFromDatabaseList(PS2GameID.Replace(".", "").Replace("_", "-").Trim())
            If PS2TitleTextBox.Text = "" Then FillTitleFromRedump("PS2", PS2GameID, PS2TitleTextBox)
            PS2NPTitleTextBox.Text = PS2GameID.Replace(".", "").Replace("_", "").Trim()
            SelectedDisc1TextBox.Text = IsoFile
        ElseIf MsgBox(Localizer.T("Could not find the PS2 game ID within the ISO file.\nDo you want to use this file anyway ?"), MsgBoxStyle.YesNo, Localizer.T("PS2 Game ID not found")) = MsgBoxResult.Yes Then
            SelectedDisc1TextBox.Text = IsoFile
        End If
    End Function

    Private Shared Sub ShowDatabaseOption(Box As CheckBox, Available As Boolean)
        Box.IsChecked = Available
        Box.Visibility = If(Available, Visibility.Visible, Visibility.Hidden)
    End Sub

    Private Sub BrowsePS2IconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2IconButton.Click
        Dim Image As String = PickImage(True)
        If Image IsNot Nothing Then SelectedIconTextBox.Text = Image
    End Sub

    Private Sub BrowsePS2StartupImageButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2StartupImageButton.Click
        Dim Image As String = PickImage(False)
        If Image IsNot Nothing Then SelectedStartupImageTextBox.Text = Image
    End Sub

    Private Async Sub PS2FindCoverButton_Click(sender As Object, e As RoutedEventArgs) Handles PS2FindCoverButton.Click
        Await FindCover("PS2", If(CurrentPS2GameID <> "", CurrentPS2GameID, PS2NPTitleTextBox.Text), PS2TitleTextBox.Text, SelectedIconTextBox, SelectedStartupImageTextBox, PS2FindCoverButton)
    End Sub

    Private Sub BrowsePS2MCButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2MCButton.Click
        Dim Card As String = PickFile("Select a PS2 Memory Card file.", "VM2 Card (*.vm2)|*.vm2|PS2 Card (*.ps2)|*.ps2|BIN Card (*.bin)|*.bin")
        If Card IsNot Nothing Then SelectedPS2MemoryCardTextBox.Text = Card
    End Sub

    Private Sub BrowsePS2TXTConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2TXTConfigButton.Click
        Dim ConfigFile As String = PickFile("Select a TXT config file.", "TXT (*.txt)|*.txt")
        If ConfigFile IsNot Nothing Then SelectedPS2TXTConfigTextBox.Text = ConfigFile
    End Sub

    Private Sub BrowsePS2LUAConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2LUAConfigButton.Click
        Dim ConfigFile As String = PickFile("Select a LUA config file.", "LUA (*.lua)|*.lua")
        If ConfigFile IsNot Nothing Then SelectedPS2LUAConfigTextBox.Text = ConfigFile
    End Sub

    Private Sub BrowsePS2Disc2Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc2Button.Click
        Dim IsoFile As String = PickFile("Select a PS2 ISO file.", "ISO (*.iso)|*.iso")
        If IsoFile IsNot Nothing Then SelectedDisc2TextBox.Text = IsoFile
    End Sub

    Private Sub BrowsePS2Disc3Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc3Button.Click
        Dim IsoFile As String = PickFile("Select a PS2 ISO file.", "ISO (*.iso)|*.iso")
        If IsoFile IsNot Nothing Then SelectedDisc3TextBox.Text = IsoFile
    End Sub

    Private Sub BrowsePS2Disc4Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc4Button.Click
        Dim IsoFile As String = PickFile("Select a PS2 ISO file.", "ISO (*.iso)|*.iso")
        If IsoFile IsNot Nothing Then SelectedDisc4TextBox.Text = IsoFile
    End Sub

    Private Sub BrowsePS2Disc5Button_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePS2Disc5Button.Click
        Dim IsoFile As String = PickFile("Select a PS2 ISO file.", "ISO (*.iso)|*.iso")
        If IsoFile IsNot Nothing Then SelectedDisc5TextBox.Text = IsoFile
    End Sub

    Private Function CreatePS2Request() As PS2BuildRequest
        Return New PS2BuildRequest() With {
            .Title = PS2TitleTextBox.Text,
            .NPTitle = PS2NPTitleTextBox.Text,
            .DetectedGameID = CurrentPS2GameID,
            .GameCRC = CurrentPS2GameCRC,
            .Disc1 = SelectedDisc1TextBox.Text,
            .Disc2 = SelectedDisc2TextBox.Text,
            .Disc3 = SelectedDisc3TextBox.Text,
            .Disc4 = SelectedDisc4TextBox.Text,
            .Disc5 = SelectedDisc5TextBox.Text,
            .Emulator = PS2EmulatorComboBox.Text,
            .IconFile = SelectedIconTextBox.Text,
            .BackgroundFile = SelectedStartupImageTextBox.Text,
            .MemoryCardFile = SelectedPS2MemoryCardTextBox.Text,
            .TxtConfigFile = SelectedPS2TXTConfigTextBox.Text,
            .LuaConfigFile = SelectedPS2LUAConfigTextBox.Text,
            .AddTxtConfigFromDatabase = (PS2AddTXTConfigFromDatabaseCheckBox.IsChecked = True),
            .AddLuaConfigFromDatabase = (PS2AddLUAConfigFromDatabaseCheckBox.IsChecked = True),
            .AddPS3ConfigFromDatabase = (PS2AddPS3ConfigFromDatabaseCheckBox.IsChecked = True),
            .UseWidescreenPatch = (PS2UseWidescreenPatchCheckBox.IsChecked = True),
            .Uprender = PS2UprenderComboBox.Text,
            .Upscaling = PS2UpscalingComboBox.Text,
            .DisplayMode = PS2DisplayModeComboBox.Text,
            .RestartEmulatorOnDiscChange = (PS2RestartEmulatorOnDiscChangeCheckBox.IsChecked = True),
            .MultitapIndex = PS2MultitapComboBox.SelectedIndex,
            .ImproveSpeed = (PS2ImproveSpeedCheckBox.IsChecked = True),
            .FixGraphics = (PS2FixGraphicsCheckBox.IsChecked = True),
            .DisableMTVU = (PS2DisableMTVUCheckBox.IsChecked = True),
            .DisableInstantVIF1Transfer = (PS2DisableInstantVIF1TransferCheckBox.IsChecked = True),
            .UseCompression = (PS2UseCompressionCheckBox.IsChecked = True)
        }
    End Function

    Private Async Sub BuildPS2fPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPS2fPKGButton.Click
        Dim Request As PS2BuildRequest = CreatePS2Request()
        Await RunBuild("PS2", Request.Title, Request.NPTitle, BuildHistory.Summarize(Request),
                       Function(Folder) BuildPreflight.CheckPS2(Request, Folder),
                       Function(Folder) PS2PackageBuilder.Build(Request, Folder),
                       BuildPS2fPKGButton, PS2BuildProgress)
    End Sub

#End Region

#Region "PSP"

    Private Sub BrowsePSPIconButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPIconButton.Click
        Dim Image As String = PickImage(True)
        If Image IsNot Nothing Then SelectedPSPIconTextBox.Text = Image
    End Sub

    Private Sub BrowsePSPBGButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPBGButton.Click
        Dim Image As String = PickImage(False)
        If Image IsNot Nothing Then SelectedPSPBGImageTextBox.Text = Image
    End Sub

    Private Async Sub PSPDiscImagesButton_Click(sender As Object, e As RoutedEventArgs) Handles PSPDiscImagesButton.Click
        Dim IsoFile As String = SelectedPSPDiscTextBox.Text
        If String.IsNullOrWhiteSpace(IsoFile) OrElse Not File.Exists(IsoFile) Then
            MsgBox(Localizer.T("Select the ISO file of the game first."), MsgBoxStyle.Information, Localizer.T("Images from the disc"))
            Return
        End If
        Dim Name As String = If(PSPNPTitleTextBox.Text <> "", PSPNPTitleTextBox.Text, Path.GetFileNameWithoutExtension(IsoFile))
        Dim Images As CoverArt.GeneratedImages = Await Task.Run(Function() CoverArt.CreateImagesFromPspIso(IsoFile, Name))
        If Images Is Nothing Then
            MsgBox(Localizer.T("This disc has no icon. Choose an image yourself."), MsgBoxStyle.Information, Localizer.T("Images from the disc"))
            Return
        End If
        ApplyImages(Images, SelectedPSPIconTextBox, SelectedPSPBGImageTextBox)
    End Sub

    Private Async Sub BrowsePSPDiscButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPDiscButton.Click
        Dim IsoFile As String = PickFile("Select a PSP ISO file.", "PSP image (*.iso;*.cso)|*.iso;*.cso")
        If IsoFile IsNot Nothing Then Await LoadPSPDisc(IsoFile)
    End Sub

    Private Async Function LoadPSPDisc(IsoFile As String) As Task
        'A compressed .cso is turned into a normal .iso first (kept in the cache folder)
        If Path.GetExtension(IsoFile).Equals(".cso", StringComparison.OrdinalIgnoreCase) Then
            Dim CsoFile As String = IsoFile
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait
            Try
                IsoFile = Await Task.Run(Function() CsoReader.ConvertToCache(CsoFile))
            Catch ex As Exception
                MsgBox(Localizer.F("The .cso file could not be converted: {0}", ex.Message), MsgBoxStyle.Exclamation)
                Return
            Finally
                Mouse.OverrideCursor = Nothing
            End Try
        End If
        If IsoTools.FileExistInISO(IsoFile, "\PSP_GAME\PARAM.SFO") Then
            Directory.CreateDirectory(AppPaths.CacheDirectory)
            Dim TempUMDDataFile As String = Path.Combine(AppPaths.CacheDirectory, "temp_umd_data.bin")
            Dim ExtractedUMDDataPath As String = IsoTools.ExtractFileFromPSPISO(IsoFile, "UMD_DATA.BIN", TempUMDDataFile)

            If Not String.IsNullOrEmpty(ExtractedUMDDataPath) Then
                PSPNPTitleTextBox.Text = Text.Encoding.ASCII.GetString(PspTools.ReadBytes(TempUMDDataFile, 0, 10)).Replace("-", "")
                PSPTitleTextBox.Text = Regex.Replace(Path.GetFileNameWithoutExtension(IsoFile), "\((.*?)\)", "")
                PSPTitleTextBox.Text = Regex.Replace(PSPTitleTextBox.Text, " {2,}", "")
                'The title stored in the disc is better than the one guessed from the file name
                Dim DiscTitle As String = IsoTools.GetPSPTitle(IsoFile)
                If DiscTitle <> "" Then PSPTitleTextBox.Text = DiscTitle
                File.Delete(TempUMDDataFile)
            End If

            SelectedPSPDiscTextBox.Text = IsoFile
        ElseIf MsgBox(Localizer.T("Could not find any PSP game information within the ISO file.\nDo you want to use this file anyway ?"), MsgBoxStyle.YesNo, Localizer.T("Unknown PSP ISO file")) = MsgBoxResult.Yes Then
            SelectedPSPDiscTextBox.Text = IsoFile
        End If
    End Function

    Private Sub BrowsePSPConfigButton_Click(sender As Object, e As RoutedEventArgs) Handles BrowsePSPConfigButton.Click
        Dim ConfigFile As String = PickFile("Select a config-title.txt file.", "TXT (*.txt)|*.txt")
        If ConfigFile IsNot Nothing Then SelectedPSPConfigTextBox.Text = ConfigFile
    End Sub

    Private Function CreatePSPRequest() As PSPBuildRequest
        Return New PSPBuildRequest() With {
            .Title = PSPTitleTextBox.Text,
            .NPTitle = PSPNPTitleTextBox.Text,
            .IsoFile = SelectedPSPDiscTextBox.Text,
            .IconFile = SelectedPSPIconTextBox.Text,
            .BackgroundFile = SelectedPSPBGImageTextBox.Text,
            .ConfigFile = SelectedPSPConfigTextBox.Text,
            .Warn = AddressOf WarnFromBuild
        }
    End Function

    Private Async Sub BuildPSPfPKGButton_Click(sender As Object, e As RoutedEventArgs) Handles BuildPSPfPKGButton.Click
        Dim Request As PSPBuildRequest = CreatePSPRequest()
        Await RunBuild("PSP", Request.Title, Request.NPTitle, BuildHistory.Summarize(Request),
                       Function(Folder) BuildPreflight.CheckPSP(Request, Folder),
                       Function(Folder) PSPPackageBuilder.Build(Request, Folder),
                       BuildPSPfPKGButton, PSPBuildProgress)
    End Sub

#End Region

End Class
