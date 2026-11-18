Imports System.IO

''' <summary>A file found in an imported archive, and what it is for the app (a disc, a config, an image...).</summary>
Public Class ImportedFile
    Public Property Path As String = ""
    Public Property Kind As DroppedFileKind
End Class

''' <summary>
''' Opens a .zip or .7z that the user sent to the app: extracts it and finds the game files in it, so the app can fill
''' the fields itself. Works for PS1, PS2 and PSP. The files are then handled like files dropped on the window.
''' </summary>
Public Class ArchiveImport

    ''' <summary>Folder under the cache where the archives are extracted (one folder per archive).</summary>
    Public Shared ReadOnly Property ExtractDirectory As String
        Get
            Return Path.Combine(AppPaths.CacheDirectory, "Imported")
        End Get
    End Property

    Public Shared Function IsArchive(FilePath As String) As Boolean
        If String.IsNullOrWhiteSpace(FilePath) Then Return False
        Dim Extension As String = Path.GetExtension(FilePath)
        Return Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) OrElse Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>
    ''' Extracts an archive into its own folder under <see cref="ExtractDirectory"/> (the old extraction of the same archive is replaced)
    ''' and returns that folder. Uses 7z from the Tools folder (through Wine on Linux and macOS).
    ''' </summary>
    Public Shared Function Extract(ArchivePath As String, Optional DestinationFolder As String = Nothing) As String
        If Not File.Exists(ArchivePath) Then Throw New FileNotFoundException(Localizer.T("The archive was not found."), ArchivePath)
        If Not IsArchive(ArchivePath) Then Throw New InvalidOperationException(Localizer.T("Only .zip and .7z archives can be opened."))

        Dim Folder As String = If(DestinationFolder, Path.Combine(ExtractDirectory, Path.GetFileNameWithoutExtension(ArchivePath)))
        If Directory.Exists(Folder) Then Directory.Delete(Folder, True)
        Directory.CreateDirectory(Folder)

        Dim Result As ProcessResult = ToolRunner.Run(Path.Combine(AppPaths.ToolsDirectory, "7z.exe"),
                                                     {"x", "-y", "-o" + Platform.ToolPath(Folder), Platform.ToolPath(ArchivePath)})
        'Exit codes of 7z: 0 = done, 1 = done with warnings, 2 and more = failed
        If Result.ExitCode >= 2 Then
            Throw New InvalidOperationException(Localizer.T("The archive could not be extracted.") + vbCrLf + Result.Output + Result.ErrorOutput)
        End If
        Return Folder
    End Function

    ''' <summary>
    ''' The game files of an extracted folder, found in any sub folder. A disc with one BIN file per track is given once: its
    ''' CUE sheet's first BIN file (the other tracks belong to it and are not listed).
    ''' </summary>
    Public Shared Function FindGameFiles(Folder As String) As List(Of ImportedFile)
        Dim AllFiles As List(Of String) = Directory.GetFiles(Folder, "*", SearchOption.AllDirectories).OrderBy(Function(Name) Name, StringComparer.OrdinalIgnoreCase).ToList()

        'Track files of the CUE sheets (every BIN after the first one of a sheet) are part of the disc, not discs of their own
        Dim TrackFiles As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim CueFiles As New List(Of String)
        For Each File As String In AllFiles
            If Not Path.GetExtension(File).Equals(".cue", StringComparison.OrdinalIgnoreCase) Then Continue For
            CueFiles.Add(File)
            Dim Parts As List(Of String) = DiscImages.ReferencedFiles(File).Select(Function(Name) Path.Combine(Path.GetDirectoryName(File), Name)).ToList()
            For Each Track As String In Parts.Skip(1)
                TrackFiles.Add(Track)
            Next
        Next

        Dim Result As New List(Of ImportedFile)
        'Each CUE sheet gives its first BIN file as the disc
        For Each Cue As String In CueFiles
            Dim Bin As String = DroppedFiles.DiscFile(Cue)
            If Bin <> "" Then Result.Add(New ImportedFile With {.Path = Bin, .Kind = DroppedFileKind.PS1Disc})
        Next
        For Each File As String In AllFiles
            If TrackFiles.Contains(File) Then Continue For
            If Path.GetExtension(File).Equals(".cue", StringComparison.OrdinalIgnoreCase) Then Continue For
            'A .bin of a CUE sheet is already listed as the disc
            If Result.Any(Function(Found) String.Equals(Found.Path, File, StringComparison.OrdinalIgnoreCase)) Then Continue For
            Dim Kind As DroppedFileKind = DroppedFiles.Classify(File)
            'Text files (readme, release notes such as "Vimm's Lair.txt") are not emulator configs: the user picks those
            If Kind = DroppedFileKind.Unknown OrElse Kind = DroppedFileKind.TxtConfig OrElse Kind = DroppedFileKind.LuaConfig Then Continue For
            Result.Add(New ImportedFile With {.Path = File, .Kind = Kind})
        Next
        Return Result
    End Function

End Class
