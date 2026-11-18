Imports System.IO

''' <summary>A file or folder listed in an archive.</summary>
Public Class ArchiveEntry
    ''' <summary>Path inside the archive, with "/" separators.</summary>
    Public Property Path As String = ""
    Public Property Size As Long
    Public Property IsFolder As Boolean
    Public Property IsEncrypted As Boolean
End Class

Public Enum ArchiveIssueKind
    NoGameFiles
    MissingTrack
    Encrypted
    NotEnoughSpace
End Enum

''' <summary>A problem found in an archive before it is extracted (the message is already translated).</summary>
Public Class ArchiveIssue
    Public Property Kind As ArchiveIssueKind
    Public Property Message As String = ""
End Class

''' <summary>What a look inside an archive found. Every issue stops the import: extracting would only waste time and disk space.</summary>
Public Class ArchiveReport
    Public Property Entries As New List(Of ArchiveEntry)
    Public Property Issues As New List(Of ArchiveIssue)

    Public ReadOnly Property TotalBytes As Long
        Get
            Return Entries.Where(Function(Item) Not Item.IsFolder).Sum(Function(Item) Item.Size)
        End Get
    End Property

    Public ReadOnly Property IsOk As Boolean
        Get
            Return Issues.Count = 0
        End Get
    End Property

    ''' <summary>The messages of all issues, one per line.</summary>
    Public Function Describe() As String
        Return String.Join(vbLf, Issues.Select(Function(Item) Item.Message))
    End Function
End Class

''' <summary>
''' Looks inside a .zip or .7z before it is extracted, and says what is wrong: no game files, a .cue sheet whose track
''' files are not in the archive, a password, or not enough free disk space. Only the small .cue files are extracted.
''' </summary>
Public Class ArchiveInspector

    Private Shared ReadOnly GameExtensions As String() = {".bin", ".cue", ".iso", ".cso"}

    ''' <summary>
    ''' The entries of the listing printed by "7z l -slt": a header block, a line of dashes, then one block per entry
    ''' ("Path = ...", "Folder = +/-", "Size = ...", "Encrypted = +/-"), separated by blank lines.
    ''' </summary>
    Public Shared Function ParseListing(Listing As String) As List(Of ArchiveEntry)
        Dim Entries As New List(Of ArchiveEntry)
        Dim Current As ArchiveEntry = Nothing
        Dim InEntries As Boolean = False
        For Each RawLine As String In If(Listing, "").Split({vbLf}, StringSplitOptions.None)
            Dim Line As String = RawLine.TrimEnd(vbCr)
            If Not InEntries Then
                If Line.StartsWith("----------") Then InEntries = True
                Continue For
            End If
            If Line = "" Then
                If Current IsNot Nothing Then Entries.Add(Current)
                Current = Nothing
                Continue For
            End If
            Dim Separator As Integer = Line.IndexOf(" = ", StringComparison.Ordinal)
            If Separator < 0 Then Continue For
            Dim Key As String = Line.Substring(0, Separator)
            Dim Value As String = Line.Substring(Separator + 3)
            If Key = "Path" Then
                If Current IsNot Nothing Then Entries.Add(Current)
                Current = New ArchiveEntry With {.Path = Value.Replace("\"c, "/"c)}
                If Current.Path.EndsWith("/") Then Current.IsFolder = True
            ElseIf Current IsNot Nothing Then
                Select Case Key
                    Case "Size"
                        Long.TryParse(Value, Current.Size)
                    Case "Folder"
                        If Value = "+" Then Current.IsFolder = True
                    Case "Attributes"
                        If Value.StartsWith("D") Then Current.IsFolder = True
                    Case "Encrypted"
                        Current.IsEncrypted = Value = "+"
                End Select
            End If
        Next
        If Current IsNot Nothing Then Entries.Add(Current)
        Return Entries
    End Function

    ''' <summary>
    ''' Finds the problems of an archive. <paramref name="CueFolder"/> holds the extracted .cue files (at the same relative paths as in the archive).
    ''' <paramref name="FreeBytes"/> is the free space where the archive will be extracted (a negative value skips that check).
    ''' </summary>
    Public Shared Function Analyze(Entries As List(Of ArchiveEntry), CueFolder As String, FreeBytes As Long) As ArchiveReport
        Dim Report As New ArchiveReport With {.Entries = Entries}
        Dim Files As List(Of ArchiveEntry) = Entries.Where(Function(Item) Not Item.IsFolder).ToList()

        If Files.Any(Function(Item) Item.IsEncrypted) Then
            Report.Issues.Add(New ArchiveIssue With {.Kind = ArchiveIssueKind.Encrypted,
                .Message = Localizer.T("This archive has a password. Extract it yourself and drop the game files instead.")})
            Return Report
        End If

        If Not Files.Any(Function(Item) GameExtensions.Contains(System.IO.Path.GetExtension(Item.Path).ToLowerInvariant())) Then
            Report.Issues.Add(New ArchiveIssue With {.Kind = ArchiveIssueKind.NoGameFiles,
                .Message = Localizer.T("No game files were found in this archive. A PS1 game needs its .bin and .cue files, a PS2 or PSP game its .iso file.")})
            Return Report
        End If

        'The track files that every .cue sheet lists must be in the archive
        Dim Known As New HashSet(Of String)(Files.Select(Function(Item) Item.Path), StringComparer.OrdinalIgnoreCase)
        For Each Cue As ArchiveEntry In Files.Where(Function(Item) Item.Path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase))
            Dim CueFile As String = System.IO.Path.Combine(CueFolder, Cue.Path.Replace("/"c, System.IO.Path.DirectorySeparatorChar))
            If Not File.Exists(CueFile) Then Continue For
            Dim Folder As String = If(Cue.Path.Contains("/"), Cue.Path.Substring(0, Cue.Path.LastIndexOf("/"c) + 1), "")
            For Each TrackName As String In DiscImages.ReferencedFiles(CueFile)
                If Not Known.Contains(Folder + TrackName.Replace("\"c, "/"c)) Then
                    Report.Issues.Add(New ArchiveIssue With {.Kind = ArchiveIssueKind.MissingTrack,
                        .Message = Localizer.F("The archive is incomplete: '{0}' lists the file '{1}', which is not in the archive.", System.IO.Path.GetFileName(Cue.Path), TrackName)})
                End If
            Next
        Next

        If FreeBytes >= 0 AndAlso Report.TotalBytes > FreeBytes Then
            Report.Issues.Add(New ArchiveIssue With {.Kind = ArchiveIssueKind.NotEnoughSpace,
                .Message = Localizer.F("Not enough free space to extract this archive: {0} needed, {1} free.", FormatSize(Report.TotalBytes), FormatSize(FreeBytes))})
        End If
        Return Report
    End Function

    ''' <summary>Looks inside an archive with 7z (listing, and extraction of the .cue files only).</summary>
    Public Shared Function Inspect(ArchivePath As String) As ArchiveReport
        If Not File.Exists(ArchivePath) Then Throw New FileNotFoundException(Localizer.T("The archive was not found."), ArchivePath)
        Dim SevenZip As String = System.IO.Path.Combine(AppPaths.ToolsDirectory, "7z.exe")
        Dim Listing As ProcessResult = ToolRunner.Run(SevenZip, {"l", "-slt", Platform.ToolPath(ArchivePath)})
        Dim Entries As List(Of ArchiveEntry) = ParseListing(Listing.Output)
        If Listing.ExitCode >= 2 OrElse Entries.Count = 0 Then
            Throw New InvalidOperationException(Localizer.T("The archive could not be opened. It may be damaged or not complete.") + vbCrLf + Listing.Output + Listing.ErrorOutput)
        End If

        Dim CueFolder As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "psfpkg-cues-" + Guid.NewGuid().ToString("N"))
        Try
            If Entries.Any(Function(Item) Item.Path.EndsWith(".cue", StringComparison.OrdinalIgnoreCase) AndAlso Not Item.IsEncrypted) Then
                Directory.CreateDirectory(CueFolder)
                ToolRunner.Run(SevenZip, {"x", "-y", "-o" + Platform.ToolPath(CueFolder), Platform.ToolPath(ArchivePath), "-r", "*.cue"})
            End If
            Return Analyze(Entries, CueFolder, FreeSpaceOfExtractFolder())
        Finally
            Try
                If Directory.Exists(CueFolder) Then Directory.Delete(CueFolder, True)
            Catch ex As IOException
                Debug.WriteLine("Could not remove " + CueFolder + ": " + ex.Message)
            End Try
        End Try
    End Function

    ''' <summary>Free bytes where archives are extracted, or -1 when it cannot be read (a network folder...).</summary>
    Private Shared Function FreeSpaceOfExtractFolder() As Long
        Try
            Dim Root As String = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(ArchiveImport.ExtractDirectory))
            Dim Drive As New DriveInfo(Root)
            Return If(Drive.IsReady, Drive.AvailableFreeSpace, -1)
        Catch ex As Exception
            Return -1
        End Try
    End Function

    Private Shared Function FormatSize(Bytes As Long) As String
        Return If(Bytes >= 1024L * 1024 * 1024, (Bytes / 1073741824.0).ToString("0.0") + " GB", (Bytes / 1048576.0).ToString("0") + " MB")
    End Function

End Class
