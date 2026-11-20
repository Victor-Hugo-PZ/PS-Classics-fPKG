Imports System.IO
Imports System.Text.RegularExpressions

Public Enum BatchStatus
    Waiting
    Working
    Done
    Failed
End Enum

''' <summary>One game of the queue: an archive (.zip or .7z) and what happened to it.</summary>
Public Class BatchItem
    Public Property ArchiveFile As String = ""
    Public Property Status As BatchStatus = BatchStatus.Waiting
    ''' <summary>"PS1", "PS2" or "PSP" once the game files were found.</summary>
    Public Property PlatformName As String = ""
    Public Property Title As String = ""
    Public Property NPTitle As String = ""
    ''' <summary>What the app is doing now, then the result or the reason of a failure (already translated).</summary>
    Public Property Message As String = ""
    Public Property PackagePath As String = ""
    ''' <summary>
    ''' Preset of <see cref="EmulatorPresets"/> chosen for this game only. Empty = the game follows the preset of the queue
    ''' (<see cref="BatchOptions.PresetName"/>).
    ''' </summary>
    Public Property PresetName As String = ""

    ''' <summary>The file name of the archive.</summary>
    Public ReadOnly Property DisplayName As String
        Get
            Return Path.GetFileName(ArchiveFile)
        End Get
    End Property

    ''' <summary>One line for a list: the state, the archive and what is happening or what went wrong.</summary>
    Public Function Describe() As String
        Dim Symbol As String = If(Status = BatchStatus.Done, "✔", If(Status = BatchStatus.Failed, "✘", If(Status = BatchStatus.Working, "▶", "•")))
        Dim Name As String = If(Title <> "", Title + " (" + DisplayName + ")", DisplayName)
        Dim Preset As String = If(PresetName <> "", " [" + Localizer.T(PresetName) + "]", "")
        Return Symbol + " " + Name + Preset + If(Message <> "", " - " + Message, "")
    End Function
End Class

''' <summary>The list of games of the queue window: adding archives, removing, and the summary line.</summary>
Public Class BatchQueue

    Public ReadOnly Property Items As New List(Of BatchItem)

    ''' <summary>Adds the .zip and .7z files that are not in the queue yet. Returns how many were added.</summary>
    Public Function Add(Files As IEnumerable(Of String)) As Integer
        Dim Added As Integer = 0
        For Each File As String In Files
            If Not ArchiveImport.IsArchive(File) Then Continue For
            If Items.Any(Function(Known) String.Equals(Known.ArchiveFile, File, StringComparison.OrdinalIgnoreCase)) Then Continue For
            Items.Add(New BatchItem With {.ArchiveFile = File})
            Added += 1
        Next
        Return Added
    End Function

    ''' <summary>
    ''' Gives games their own preset (an empty name puts them back to the preset of the queue). Games that are done or
    ''' being built keep theirs. Returns how many games were changed.
    ''' </summary>
    Public Function SetPreset(Selected As IEnumerable(Of BatchItem), PresetName As String) As Integer
        Dim Changed As Integer = 0
        For Each Item As BatchItem In Selected
            If Item.Status = BatchStatus.Done OrElse Item.Status = BatchStatus.Working Then Continue For
            Item.PresetName = If(PresetName, "")
            Changed += 1
        Next
        Return Changed
    End Function

    ''' <summary>Removes the games that are done. Returns how many were removed.</summary>
    Public Function ClearDone() As Integer
        Return Items.RemoveAll(Function(Item) Item.Status = BatchStatus.Done)
    End Function

    ''' <summary>Games that still have to be built (waiting or failed: a failed game can be tried again).</summary>
    Public Function ToBuild() As List(Of BatchItem)
        Return Items.Where(Function(Item) Item.Status = BatchStatus.Waiting OrElse Item.Status = BatchStatus.Failed).ToList()
    End Function

    ''' <summary>Puts the games that failed back in the waiting state, so the queue builds them again.</summary>
    Public Sub ResetFailed()
        For Each Item As BatchItem In Items.Where(Function(Candidate) Candidate.Status = BatchStatus.Failed)
            Item.Status = BatchStatus.Waiting
            Item.Message = ""
        Next
    End Sub

    ''' <summary>"2 of 3 packages were made."</summary>
    Public Function Summary() As String
        Return Localizer.F("{0} of {1} packages were made.", Items.Where(Function(Item) Item.Status = BatchStatus.Done).Count(), Items.Count)
    End Function

End Class

''' <summary>How the queue builds the games.</summary>
Public Class BatchOptions
    ''' <summary>Where the packages are written.</summary>
    Public Property OutputFolder As String = ""
    ''' <summary>Name of a preset of <see cref="EmulatorPresets"/> ("Recommended" by default).</summary>
    Public Property PresetName As String = "Recommended"
    ''' <summary>Look for the PS1/PS2 cover on the internet. Off, the game gets no icon and background.</summary>
    Public Property FindCovers As Boolean = True
    ''' <summary>Look for the title of a game that is not in the title list on the internet.</summary>
    Public Property FindTitles As Boolean = True
    ''' <summary>Remove the extracted files of a game after its package was made (they can be 700 MB).</summary>
    Public Property CleanUp As Boolean = True
    ''' <summary>Folder for the extracted files (default: <see cref="ArchiveImport.ExtractDirectory"/>).</summary>
    Public Property WorkFolder As String = Nothing
    ''' <summary>Cache folder of the covers and the Redump names (default: the covers folder of the app).</summary>
    Public Property CoverCacheFolder As String = Nothing
    ''' <summary>Called with the item whenever its status or message changes (any thread).</summary>
    Public Property Report As Action(Of BatchItem)
End Class

''' <summary>
''' Builds a whole queue of games with no questions: each archive is checked, extracted, its files found, ID and title
''' detected, the cover looked for, and the package built with the emulator settings of a preset. A game that fails does
''' not stop the others. A libcrypt protected PS1 game gets its patch (the safe choice).
''' </summary>
Public Class BatchBuilder

    ''' <summary>Builds the queue in order. Returns the number of packages that were made.</summary>
    Public Shared Function BuildAll(Items As IEnumerable(Of BatchItem), Options As BatchOptions, Optional Cancel As Threading.CancellationToken = Nothing) As Integer
        Dim Made As Integer = 0
        For Each Item As BatchItem In Items
            If Cancel.IsCancellationRequested Then Exit For
            If Item.Status = BatchStatus.Done Then Continue For
            BuildOne(Item, Options)
            If Item.Status = BatchStatus.Done Then Made += 1
        Next
        Return Made
    End Function

    ''' <summary>Builds one game of the queue. Never throws: a problem is written in the item.</summary>
    Public Shared Sub BuildOne(Item As BatchItem, Options As BatchOptions)
        Dim Folder As String = Nothing
        Try
            Update(Item, Options, BatchStatus.Working, Localizer.T("Looking inside the archive…"))
            Dim Report As ArchiveReport = ArchiveInspector.Inspect(Item.ArchiveFile)
            If Not Report.IsOk Then
                Update(Item, Options, BatchStatus.Failed, Report.Describe())
                Return
            End If

            Update(Item, Options, BatchStatus.Working, Localizer.T("Extracting…"))
            Dim Target As String = If(Options.WorkFolder Is Nothing, Nothing, Path.Combine(Options.WorkFolder, Path.GetFileNameWithoutExtension(Item.ArchiveFile)))
            Folder = ArchiveImport.Extract(Item.ArchiveFile, Target)
            Dim Files As List(Of ImportedFile) = ArchiveImport.FindGameFiles(Folder)

            Dim Result As PackageBuildResult = Nothing
            Dim Discs As List(Of ImportedFile) = Files.Where(Function(Found) Found.Kind = DroppedFileKind.PS1Disc).ToList()
            If Discs.Count > 0 Then
                Result = BuildPS1(Item, Options, Discs.Select(Function(Found) Found.Path).ToList())
            ElseIf Files.Any(Function(Found) Found.Kind = DroppedFileKind.PS2Disc) Then
                Result = BuildPS2(Item, Options, Files.Where(Function(Found) Found.Kind = DroppedFileKind.PS2Disc).Select(Function(Found) Found.Path).ToList())
            ElseIf Files.Any(Function(Found) Found.Kind = DroppedFileKind.PSPDisc) Then
                Result = BuildPSP(Item, Options, Files.First(Function(Found) Found.Kind = DroppedFileKind.PSPDisc).Path)
            Else
                Update(Item, Options, BatchStatus.Failed, Localizer.T("No game files were found in this archive. A PS1 game needs its .bin and .cue files, a PS2 or PSP game its .iso file."))
                Return
            End If

            If Result IsNot Nothing AndAlso Result.Success Then
                Item.PackagePath = Result.PackagePath
                Update(Item, Options, BatchStatus.Done, If(Result.PackagePath <> "", Path.GetFileName(Result.PackagePath), Localizer.T("Package created.")))
            Else
                Dim Friendly As FriendlyError = ErrorExplainer.Explain(If(Result?.Output, ""))
                Update(Item, Options, BatchStatus.Failed, Friendly.Explanation + " → " + Friendly.Suggestion)
            End If
        Catch ex As Exception
            Dim Friendly As FriendlyError = ErrorExplainer.Explain(ex.Message)
            Update(Item, Options, BatchStatus.Failed, Friendly.Explanation + " (" + ex.Message.Split({vbLf}, StringSplitOptions.None)(0).Trim() + ")")
        Finally
            If Options.CleanUp AndAlso Folder IsNot Nothing Then
                Try
                    If Directory.Exists(Folder) Then Directory.Delete(Folder, True)
                Catch ex As IOException
                    Debug.WriteLine("Could not remove " + Folder + ": " + ex.Message)
                End Try
            End If
        End Try
    End Sub

    Private Shared Sub Update(Item As BatchItem, Options As BatchOptions, Status As BatchStatus, Message As String)
        Item.Status = Status
        Item.Message = Message
        If Options.Report IsNot Nothing Then Options.Report.Invoke(Item)
    End Sub

#Region "Platforms"

    ''' <summary>The PS1 preset of a game: its own, else the one of the queue, else "Recommended" (a name that does not exist for PS1 is skipped).</summary>
    Public Shared Function ResolvePS1Preset(Item As BatchItem, Options As BatchOptions) As PS1Preset
        For Each Name As String In {Item.PresetName, Options.PresetName}
            Dim Found As PS1Preset = EmulatorPresets.PS1.FirstOrDefault(Function(Candidate) Candidate.Name = Name)
            If Found IsNot Nothing Then Return Found
        Next
        Return EmulatorPresets.PS1(0)
    End Function

    ''' <summary>The PS2 preset of a game (same rule as <see cref="ResolvePS1Preset"/>).</summary>
    Public Shared Function ResolvePS2Preset(Item As BatchItem, Options As BatchOptions) As PS2Preset
        For Each Name As String In {Item.PresetName, Options.PresetName}
            Dim Found As PS2Preset = EmulatorPresets.PS2.FirstOrDefault(Function(Candidate) Candidate.Name = Name)
            If Found IsNot Nothing Then Return Found
        Next
        Return EmulatorPresets.PS2(0)
    End Function

    Private Shared Function BuildPS1(Item As BatchItem, Options As BatchOptions, DiscFiles As List(Of String)) As PackageBuildResult
        Dim Preset As PS1Preset = ResolvePS1Preset(Item, Options)

        Dim GameID As String = PS1Game.GetPS1GameID(DiscFiles(0))
        Dim Title As String = If(GameID.Length = 10, PS1Game.GetPS1GameTitleFromDatabaseList(GameID), "")
        If Title = "" Then Title = TitleOfUnknown(Options, "PS1", GameID, DiscFiles(0), Item.ArchiveFile)
        Dim NPTitle As String = GameID.Replace("-", "").Trim()
        If NPTitle = "" Then NPTitle = Regex.Replace(Path.GetFileNameWithoutExtension(Item.ArchiveFile).ToUpperInvariant(), "[^A-Z0-9]", "").PadRight(9, "0"c).Substring(0, 9)
        Item.PlatformName = "PS1"
        Item.Title = Title
        Item.NPTitle = NPTitle

        Dim Images As CoverArt.GeneratedImages = FindImages(Item, Options, "PS1", GameID, Title)
        Dim Request As New PS1BuildRequest With {
            .Title = Title, .NPTitle = NPTitle, .DetectedGameID = GameID,
            .Disc1 = DiscFiles(0), .Disc2 = Nth(DiscFiles, 1), .Disc3 = Nth(DiscFiles, 2), .Disc4 = Nth(DiscFiles, 3),
            .IconFile = Images?.IconFile, .BackgroundFile = Images?.BackgroundFile,
            .Upscaling = Preset.Upscaling, .SkipBootlogo = Preset.SkipBootlogo, .Force60Hz = Preset.Force60Hz, .EmulateAnalogSticks = Preset.EmulateAnalogSticks,
            .ConfirmLibcrypt = Function() True
        }
        Return Build(Item, Options, BuildPreflight.CheckPS1(Request, Options.OutputFolder), Function() PS1PackageBuilder.Build(Request, Options.OutputFolder))
    End Function

    Private Shared Function BuildPS2(Item As BatchItem, Options As BatchOptions, DiscFiles As List(Of String)) As PackageBuildResult
        Dim Preset As PS2Preset = ResolvePS2Preset(Item, Options)

        Dim GameID As String = PS2Game.GetPS2GameID(DiscFiles(0))
        Dim GameCRC As String = ""
        Try
            GameCRC = IsoTools.GetGameCRC(IsoTools.GetELFfromISO(DiscFiles(0), GameID))
        Catch ex As Exception
            GameCRC = ""
        End Try
        Dim NPTitle As String = GameID.Replace(".", "").Replace("_", "").Trim()
        Dim Title As String = If(GameID <> "", PS2Game.GetPS2GameTitleFromDatabaseList(GameID.Replace(".", "").Replace("_", "-").Trim()), "")
        If Title = "" Then Title = TitleOfUnknown(Options, "PS2", GameID, DiscFiles(0), Item.ArchiveFile)
        If NPTitle = "" Then NPTitle = Regex.Replace(Path.GetFileNameWithoutExtension(Item.ArchiveFile).ToUpperInvariant(), "[^A-Z0-9]", "").PadRight(9, "0"c).Substring(0, 9)
        Item.PlatformName = "PS2"
        Item.Title = Title
        Item.NPTitle = NPTitle

        Dim Images As CoverArt.GeneratedImages = FindImages(Item, Options, "PS2", GameID, Title)
        Dim Request As New PS2BuildRequest With {
            .Title = Title, .NPTitle = NPTitle, .DetectedGameID = GameID, .GameCRC = GameCRC,
            .Disc1 = DiscFiles(0), .Disc2 = Nth(DiscFiles, 1), .Disc3 = Nth(DiscFiles, 2), .Disc4 = Nth(DiscFiles, 3), .Disc5 = Nth(DiscFiles, 4),
            .Emulator = Preset.Emulator, .IconFile = Images?.IconFile, .BackgroundFile = Images?.BackgroundFile,
            .Uprender = Preset.Uprender, .Upscaling = Preset.Upscaling, .DisplayMode = Preset.DisplayMode,
            .FixGraphics = Preset.FixGraphics, .ImproveSpeed = Preset.ImproveSpeed, .DisableMTVU = Preset.DisableMTVU,
            .DisableInstantVIF1Transfer = Preset.DisableInstantVIF1Transfer, .UseCompression = Preset.UseCompression
        }

        'The configurations that the database has for this game (the form checks the same boxes by itself)
        If GameID <> "" AndAlso GameCRC <> "" Then
            Dim ConfigsDirectory As String = Path.Combine(AppPaths.ToolsDirectory, "PS4", "ps2-configs")
            Request.AddTxtConfigFromDatabase = IsoTools.IsConfigAvailable(GameID, Path.Combine(ConfigsDirectory, "configs_txt.dat"))
            Request.AddLuaConfigFromDatabase = IsoTools.IsConfigAvailable(GameID, Path.Combine(ConfigsDirectory, "configs_lua.dat"))
            Request.AddPS3ConfigFromDatabase = IsoTools.IsConfigAvailable(GameID + ".CONFIG", Path.Combine(ConfigsDirectory, "configs_ps3.dat"))
            Request.UseWidescreenPatch = Preset.WidescreenPatchWhenAvailable AndAlso IsoTools.IsConfigAvailable(GameCRC + ".lua", Path.Combine(ConfigsDirectory, "widescreen.dat"))
        End If
        Return Build(Item, Options, BuildPreflight.CheckPS2(Request, Options.OutputFolder), Function() PS2PackageBuilder.Build(Request, Options.OutputFolder))
    End Function

    Private Shared Function BuildPSP(Item As BatchItem, Options As BatchOptions, IsoFile As String) As PackageBuildResult
        Dim NPTitle As String = ""
        Dim TempUmd As String = Path.Combine(AppPaths.CacheDirectory, "temp_umd_data.bin")
        If IsoTools.ExtractFileFromPSPISO(IsoFile, "UMD_DATA.BIN", TempUmd) <> "" Then
            NPTitle = Text.Encoding.ASCII.GetString(PspTools.ReadBytes(TempUmd, 0, 10)).Replace("-", "").Trim()
            File.Delete(TempUmd)
        End If
        Dim Title As String = IsoTools.GetPSPTitle(IsoFile)
        If Title = "" Then Title = TitleFromFileName(Path.GetFileNameWithoutExtension(IsoFile))
        Item.PlatformName = "PSP"
        Item.Title = Title
        Item.NPTitle = NPTitle

        Dim Images As CoverArt.GeneratedImages = Nothing
        Try
            Images = CoverArt.CreateImagesFromPspIso(IsoFile, If(NPTitle <> "", NPTitle, "PSP"), If(Options.CoverCacheFolder, CoverArt.CoversDirectory))
        Catch ex As Exception
            Images = Nothing
        End Try
        Dim Request As New PSPBuildRequest With {
            .Title = Title, .NPTitle = NPTitle, .IsoFile = IsoFile, .IconFile = Images?.IconFile, .BackgroundFile = Images?.BackgroundFile
        }
        Return Build(Item, Options, BuildPreflight.CheckPSP(Request, Options.OutputFolder), Function() PSPPackageBuilder.Build(Request, Options.OutputFolder))
    End Function

    ''' <summary>Runs the checks of the form, then the build. A problem found by the checks is the result of the item.</summary>
    Private Shared Function Build(Item As BatchItem, Options As BatchOptions, Issues As List(Of PreflightIssue), Run As Func(Of PackageBuildResult)) As PackageBuildResult
        If Issues.Any(Function(Issue) Issue.IsError) Then
            Return New PackageBuildResult With {.Success = False, .Output = BuildPreflight.Describe(Issues.Where(Function(Issue) Issue.IsError))}
        End If
        Update(Item, Options, BatchStatus.Working, Localizer.F("Building {0}…", Item.Title))
        Return Run()
    End Function

    Private Shared Function Nth(Items As List(Of String), Index As Integer) As String
        Return If(Index < Items.Count, Items(Index), "")
    End Function

#End Region

#Region "Titles and covers"

    ''' <summary>The title of a game that is not in the title list: the Redump database, then the name of the file.</summary>
    Private Shared Function TitleOfUnknown(Options As BatchOptions, PlatformName As String, GameID As String, DiscFile As String, ArchiveFile As String) As String
        If Options.FindTitles AndAlso GameID <> "" Then
            Try
                Dim Found As String = CoverSources.FindTitleAsync(PlatformName, GameID, Options.CoverCacheFolder).GetAwaiter().GetResult()
                If Found <> "" Then Return Found
            Catch ex As Exception
                Debug.WriteLine("Title lookup failed: " + ex.Message)
            End Try
        End If
        Dim FromFile As String = TitleFromFileName(Path.GetFileNameWithoutExtension(ArchiveFile))
        Return If(FromFile <> "", FromFile, TitleFromFileName(Path.GetFileNameWithoutExtension(DiscFile)))
    End Function

    ''' <summary>"Tekken 3 (USA) (Track 1)" -> "Tekken 3".</summary>
    Public Shared Function TitleFromFileName(FileName As String) As String
        Return CoverSources.TitleFromRedumpName(FileName)
    End Function

    Private Shared Function FindImages(Item As BatchItem, Options As BatchOptions, PlatformName As String, GameID As String, Title As String) As CoverArt.GeneratedImages
        If Not Options.FindCovers Then Return Nothing
        Try
            Update(Item, Options, BatchStatus.Working, Localizer.T("Looking for the cover…"))
            Dim Found As CoverFound = CoverSources.FindCoverAsync(PlatformName, GameID, Title, Options.CoverCacheFolder).GetAwaiter().GetResult()
            If Found Is Nothing Then Return Nothing
            Dim Name As String = If(CoverArt.NormalizeSerial(GameID) <> "", CoverArt.NormalizeSerial(GameID), "cover") + "-" + Found.Source.ToString()
            Return CoverArt.CreateImages(Found.Image, Name, Nothing, Options.CoverCacheFolder)
        Catch ex As Exception
            'No cover is not a reason to skip the game
            Debug.WriteLine("Cover lookup failed: " + ex.Message)
            Return Nothing
        End Try
    End Function

#End Region

End Class
