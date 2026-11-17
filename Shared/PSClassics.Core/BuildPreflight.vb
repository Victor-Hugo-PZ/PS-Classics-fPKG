Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>One problem found before a build. Errors stop the build; warnings only ask the user.</summary>
Public Class PreflightIssue
    Public Property IsError As Boolean
    ''' <summary>What is wrong (already translated).</summary>
    Public Property Message As String = ""
    ''' <summary>How to fix it (already translated, may be empty).</summary>
    Public Property Suggestion As String = ""
End Class

''' <summary>
''' Checks a build request before the build starts, so the user gets a clear message with a fix instead of a failure
''' minutes later: required fields, files that do not exist, missing .cue files, invalid NP titles, unreadable images
''' and free disk space.
''' </summary>
Public Class BuildPreflight

    ''' <summary>Space kept free on top of the package size (emulator, images, safety margin).</summary>
    Public Const ExtraSpaceBytes As Long = 200L * 1024 * 1024

    ''' <summary>Returns the problems of an NP title (empty list when it is valid).</summary>
    Public Shared Function CheckNPTitle(NPTitle As String) As List(Of PreflightIssue)
        Dim Issues As New List(Of PreflightIssue)
        If String.IsNullOrWhiteSpace(NPTitle) Then
            Issues.Add(Problem("No NP title specified.", "Type the 9 characters of the game ID, for example SLUS00594."))
        ElseIf NPTitle.Length <> 9 Then
            Issues.Add(Problem(Localizer.F("The NP title has {0} characters, it must have exactly 9.", NPTitle.Length), "Use the game ID without _ - or ., for example SLUS00594."))
        ElseIf Not Regex.IsMatch(NPTitle, "^[A-Z0-9]{9}$") Then
            Issues.Add(Problem("The NP title can only have capital letters and digits.", "Remove spaces, _ - and . from it, for example SLUS00594."))
        ElseIf Not Regex.IsMatch(NPTitle, "^[A-Z]{4}[0-9]{5}$") Then
            Issues.Add(Warning("The NP title does not look like a game ID (4 letters and 5 digits).", "It still works, but the game ID is the usual choice, for example SLUS00594."))
        End If
        Return Issues
    End Function

    Public Shared Function CheckPS1(Request As PS1BuildRequest, OutputFolder As String) As List(Of PreflightIssue)
        Dim Issues As New List(Of PreflightIssue)
        CheckTools(Issues)
        Dim Discs As String() = {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4}
        If String.IsNullOrWhiteSpace(Request.Disc1) Then
            Issues.Add(Problem("No disc 1 specified.", "Select the .BIN file of the game in the Game section."))
        End If
        For Index As Integer = 0 To Discs.Length - 1
            Dim Disc As String = Discs(Index)
            If String.IsNullOrWhiteSpace(Disc) Then Continue For
            If Not File.Exists(Disc) Then
                Issues.Add(Problem(Localizer.F("Disc {0} was not found: {1}", Index + 1, Disc), "The file was moved or deleted. Select it again."))
            ElseIf Not File.Exists(Path.ChangeExtension(Disc, ".cue")) Then
                Issues.Add(Problem(Localizer.F("The .CUE file of disc {0} is missing.", Index + 1),
                                   Localizer.F("Put {0} next to the .BIN file (same name, .cue extension).", Path.GetFileNameWithoutExtension(Disc) + ".cue")))
            End If
        Next
        CheckTitle(Issues, Request.Title)
        Issues.AddRange(CheckNPTitle(Request.NPTitle))
        CheckImages(Issues, Request.IconFile, Request.BackgroundFile)
        CheckOptionalFile(Issues, Request.TxtConfigFile, "TXT config")
        CheckOptionalFile(Issues, Request.LuaConfigFile, "LUA config")
        CheckSpace(Issues, OutputFolder, Discs.SelectMany(Function(Disc) {Disc, If(String.IsNullOrWhiteSpace(Disc), "", Path.ChangeExtension(Disc, ".cue"))}), 0)
        Return Issues
    End Function

    Public Shared Function CheckPS2(Request As PS2BuildRequest, OutputFolder As String) As List(Of PreflightIssue)
        Dim Issues As New List(Of PreflightIssue)
        CheckTools(Issues)
        Dim Discs As String() = {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4, Request.Disc5}
        If String.IsNullOrWhiteSpace(Request.Disc1) Then
            Issues.Add(Problem("No disc 1 specified.", "Select the .ISO file of the game in the Game section."))
        End If
        For Index As Integer = 0 To Discs.Length - 1
            If Not String.IsNullOrWhiteSpace(Discs(Index)) AndAlso Not File.Exists(Discs(Index)) Then
                Issues.Add(Problem(Localizer.F("Disc {0} was not found: {1}", Index + 1, Discs(Index)), "The file was moved or deleted. Select it again."))
            End If
        Next
        CheckTitle(Issues, Request.Title)
        Issues.AddRange(CheckNPTitle(Request.NPTitle))
        CheckImages(Issues, Request.IconFile, Request.BackgroundFile)
        CheckOptionalFile(Issues, Request.TxtConfigFile, "TXT config")
        CheckOptionalFile(Issues, Request.LuaConfigFile, "LUA config")
        CheckOptionalFile(Issues, Request.MemoryCardFile, "Memory card")
        If Not String.IsNullOrWhiteSpace(Request.MemoryCardFile) AndAlso File.Exists(Request.MemoryCardFile) Then
            Dim Size As Long = New FileInfo(Request.MemoryCardFile).Length
            If Size < 8L * 1024 * 1024 OrElse Size > 9L * 1024 * 1024 Then
                Issues.Add(Warning(Localizer.F("The memory card is {0}, PS2 memory cards are 8 MB.", FormatSize(Size)), "Check that it is a PS2 memory card image (.vm2, .ps2 or .bin)."))
            End If
        End If
        CheckSpace(Issues, OutputFolder, Discs, 0)
        Return Issues
    End Function

    Public Shared Function CheckPSP(Request As PSPBuildRequest, OutputFolder As String) As List(Of PreflightIssue)
        Dim Issues As New List(Of PreflightIssue)
        CheckTools(Issues)
        If String.IsNullOrWhiteSpace(Request.IsoFile) Then
            Issues.Add(Problem("No ISO file specified.", "Select the .ISO file of the game in the Game section."))
        ElseIf Not File.Exists(Request.IsoFile) Then
            Issues.Add(Problem(Localizer.F("The ISO file was not found: {0}", Request.IsoFile), "The file was moved or deleted. Select it again."))
        End If
        CheckTitle(Issues, Request.Title)
        Issues.AddRange(CheckNPTitle(Request.NPTitle))
        CheckImages(Issues, Request.IconFile, Request.BackgroundFile)
        If String.IsNullOrWhiteSpace(Request.IconFile) Then
            Issues.Add(Warning("No icon selected: the game will have an empty tile on the home screen.", "Use 'Images from the disc' or choose an icon."))
        End If
        CheckOptionalFile(Issues, Request.ConfigFile, "Custom config")
        'The PSP ISO is also copied into the cache folder
        Dim IsoSize As Long = If(File.Exists(Request.IsoFile), New FileInfo(Request.IsoFile).Length, 0)
        CheckSpace(Issues, OutputFolder, {Request.IsoFile}, IsoSize)
        Return Issues
    End Function

    ''' <summary>"1.5 GB", "700 MB"...</summary>
    Public Shared Function FormatSize(Bytes As Long) As String
        If Bytes >= 1024L * 1024 * 1024 Then Return (Bytes / (1024.0 * 1024 * 1024)).ToString("0.0") + " GB"
        Return Math.Max(1, CLng(Math.Round(Bytes / (1024.0 * 1024)))).ToString() + " MB"
    End Function

    Private Shared Sub CheckTools(Issues As List(Of PreflightIssue))
        If Not AppPaths.ToolsAvailable() Then
            Issues.Add(Problem(Localizer.F("The Tools folder was not found at {0}.", AppPaths.ToolsDirectory), "Keep the Tools folder of the download next to the program."))
        End If
    End Sub

    Private Shared Sub CheckTitle(Issues As List(Of PreflightIssue), Title As String)
        If String.IsNullOrWhiteSpace(Title) Then
            Issues.Add(Problem("No game title specified.", "Type the name that the home screen will show."))
        ElseIf Text.Encoding.UTF8.GetByteCount(Title) > 127 Then
            Issues.Add(Problem("The title is too long.", "Use a shorter title (up to about 120 letters)."))
        End If
    End Sub

    Private Shared Sub CheckImages(Issues As List(Of PreflightIssue), IconFile As String, BackgroundFile As String)
        For Each Image In {(IconFile, "The icon"), (BackgroundFile, "The background")}
            If String.IsNullOrWhiteSpace(Image.Item1) Then Continue For
            If Not File.Exists(Image.Item1) Then
                Issues.Add(Problem(Localizer.F(Image.Item2 + " was not found: {0}", Image.Item1), "The file was moved or deleted. Select it again."))
            ElseIf ImageConverter.GetSize(Image.Item1) Is Nothing Then
                Issues.Add(Problem(Localizer.T(Image.Item2 + " could not be read."), "Use a PNG, JPG, BMP, GIF or WebP image."))
            End If
        Next
    End Sub

    Private Shared Sub CheckOptionalFile(Issues As List(Of PreflightIssue), FileName As String, FieldName As String)
        If Not String.IsNullOrWhiteSpace(FileName) AndAlso Not File.Exists(FileName) Then
            Issues.Add(Problem(Localizer.F("The file of the field '{0}' was not found: {1}", Localizer.T(FieldName), FileName), "Select it again or clear the field."))
        End If
    End Sub

    ''' <summary>The package is about as big as the game files; the PSP build also copies the ISO into the cache folder.</summary>
    Private Shared Sub CheckSpace(Issues As List(Of PreflightIssue), OutputFolder As String, InputFiles As IEnumerable(Of String), CacheBytes As Long)
        If String.IsNullOrWhiteSpace(OutputFolder) Then Return
        Dim GameBytes As Long = InputFiles.Where(Function(Name) Not String.IsNullOrWhiteSpace(Name) AndAlso File.Exists(Name)).Sum(Function(Name) New FileInfo(Name).Length)
        Try
            Dim OutputDrive As New DriveInfo(Path.GetPathRoot(Path.GetFullPath(OutputFolder)))
            Dim CacheRoot As String = Path.GetPathRoot(Path.GetFullPath(AppPaths.CacheDirectory))
            Dim Needed As Long = GameBytes + ExtraSpaceBytes
            If String.Equals(CacheRoot, OutputDrive.Name, StringComparison.OrdinalIgnoreCase) Then Needed += CacheBytes
            If OutputDrive.IsReady AndAlso OutputDrive.AvailableFreeSpace < Needed Then
                Issues.Add(Problem(Localizer.F("Not enough free space on {0}: about {1} needed, {2} free.", OutputDrive.Name, FormatSize(Needed), FormatSize(OutputDrive.AvailableFreeSpace)),
                                   "Free some space or choose an output folder on another drive."))
            End If
        Catch ex As Exception
            'Unknown drive (network folder...): the build itself reports a full disk
        End Try
    End Sub

    Private Shared Function Problem(Message As String, Suggestion As String) As PreflightIssue
        Return New PreflightIssue With {.IsError = True, .Message = Localizer.T(Message), .Suggestion = Localizer.T(Suggestion)}
    End Function

    Private Shared Function Warning(Message As String, Suggestion As String) As PreflightIssue
        Return New PreflightIssue With {.IsError = False, .Message = Localizer.T(Message), .Suggestion = Localizer.T(Suggestion)}
    End Function

    ''' <summary>Text for a dialog: errors first, each with its fix.</summary>
    Public Shared Function Describe(Issues As IEnumerable(Of PreflightIssue)) As String
        Dim Lines As New List(Of String)
        For Each Issue In Issues.OrderByDescending(Function(Item) Item.IsError)
            Lines.Add(If(Issue.IsError, "✘ ", "⚠ ") + Issue.Message)
            If Issue.Suggestion <> "" Then Lines.Add("   → " + Issue.Suggestion)
        Next
        Return String.Join(vbLf, Lines)
    End Function

End Class
