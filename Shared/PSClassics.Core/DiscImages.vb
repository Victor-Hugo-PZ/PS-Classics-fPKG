Imports System.IO
Imports System.Text.RegularExpressions

''' <summary>
''' A PS1 disc as the builder uses it: the CUE sheet and the BIN file that the package gets (after a merge, when the
''' disc has one BIN file per track).
''' </summary>
Public Class PreparedDisc
    Public Property Number As Integer
    Public Property CueFile As String = ""
    Public Property BinFile As String = ""
    ''' <summary>True when the BIN and CUE files were created by the merge (they are removed after the build).</summary>
    Public Property IsMerged As Boolean

    Public Sub DeleteMergedFiles()
        If Not IsMerged Then Return
        For Each Name As String In {BinFile, CueFile}
            Try
                If File.Exists(Name) Then File.Delete(Name)
            Catch ex As IOException
                Debug.WriteLine("Could not remove " + Name + ": " + ex.Message)
            End Try
        Next
    End Sub
End Class

''' <summary>
''' Finds the CUE sheet of a PS1 disc and merges a multi-file disc into one BIN file.
''' A game with CD audio (for example Tekken 3) has one BIN file per track, and one CUE sheet that lists them all.
''' The TOC tool (cue2toc) reads only one BIN file, so the tracks are joined into one BIN file, with the CUE indexes moved.
''' </summary>
Public Class DiscImages

    Private Const SectorSize As Integer = 2352
    Private Const FramesPerSecond As Integer = 75
    Private Const FramesPerMinute As Integer = 75 * 60

    ''' <summary>
    ''' The CUE sheet of a BIN file: the file with the same name and the .cue extension, or else the CUE sheet of the
    ''' same folder that lists the BIN file (one CUE sheet for several BIN files).
    ''' </summary>
    Public Shared Function FindCueFile(BinFile As String) As String
        Dim Direct As String = Path.ChangeExtension(BinFile, ".cue")
        If File.Exists(Direct) Then Return Direct
        Dim Folder As String = Path.GetDirectoryName(BinFile)
        Dim BinName As String = Path.GetFileName(BinFile)
        If Directory.Exists(Folder) Then
            For Each Candidate As String In Directory.GetFiles(Folder).OrderBy(Function(Name) Name, StringComparer.OrdinalIgnoreCase)
                If Not Path.GetExtension(Candidate).Equals(".cue", StringComparison.OrdinalIgnoreCase) Then Continue For
                If ReferencedFiles(Candidate).Any(Function(Name) String.Equals(Name, BinName, StringComparison.OrdinalIgnoreCase)) Then Return Candidate
            Next
        End If
        Throw New FileNotFoundException(Localizer.T("No CUE sheet lists this BIN file. Put a .cue file next to the .bin file (same name, or one that lists it)."), BinFile)
    End Function

    ''' <summary>The BIN file names of the FILE lines of a CUE sheet, in order.</summary>
    Public Shared Function ReferencedFiles(CueFile As String) As List(Of String)
        Dim Names As New List(Of String)
        For Each Line As String In File.ReadAllLines(CueFile)
            Dim Match As Match = Regex.Match(Line, "^\s*FILE\s+(?:""([^""]+)""|(\S+))\s+\S+\s*$", RegexOptions.IgnoreCase)
            If Match.Success Then Names.Add(If(Match.Groups(1).Success, Match.Groups(1).Value, Match.Groups(2).Value))
        Next
        Return Names
    End Function

    ''' <summary>
    ''' Prepares a disc for the package. A disc with one BIN file is used as it is. A disc with several BIN files is merged
    ''' into <paramref name="WorkFolder"/> ("<name>.bin" and "<name>.cue"), which the caller removes after the build.
    ''' </summary>
    Public Shared Function Prepare(Number As Integer, BinFile As String, WorkFolder As String, BaseName As String) As PreparedDisc
        Dim Cue As String = FindCueFile(BinFile)
        Dim Parts As List(Of String) = ReferencedFiles(Cue).Select(Function(Name) Path.Combine(Path.GetDirectoryName(Cue), Name)).ToList()
        If Parts.Count <= 1 Then
            Return New PreparedDisc With {.Number = Number, .CueFile = Cue, .BinFile = BinFile}
        End If

        Directory.CreateDirectory(WorkFolder)
        Dim Merged As New PreparedDisc With {.Number = Number, .IsMerged = True,
                                             .BinFile = Path.Combine(WorkFolder, BaseName + ".bin"),
                                             .CueFile = Path.Combine(WorkFolder, BaseName + ".cue")}
        Try
            MergeTracks(Cue, Merged.BinFile, Merged.CueFile)
        Catch
            Merged.DeleteMergedFiles()
            Throw
        End Try
        Return Merged
    End Function

    ''' <summary>
    ''' Joins the BIN files of a CUE sheet into <paramref name="OutputBin"/> and writes <paramref name="OutputCue"/> for it.
    ''' The first file name of the CUE sheet is kept as the file name of the merged BIN file (as a single-BIN disc has).
    ''' </summary>
    Public Shared Sub MergeTracks(CueFile As String, OutputBin As String, OutputCue As String)
        Dim Folder As String = Path.GetDirectoryName(CueFile)
        Dim Parts As List(Of String) = ReferencedFiles(CueFile).Select(Function(Name) Path.Combine(Folder, Name)).ToList()
        For Each Part As String In Parts
            If Not File.Exists(Part) Then Throw New FileNotFoundException(Localizer.T("A BIN file of the CUE sheet was not found. Every track needs its .bin file next to the .cue file."), Part)
        Next

        Dim CueLines As New List(Of String)
        Dim StartFrame As Long = 0
        Dim PartIndex As Integer = -1
        For Each Line As String In File.ReadAllLines(CueFile)
            If Regex.IsMatch(Line, "^\s*FILE\s", RegexOptions.IgnoreCase) Then
                If PartIndex >= 0 Then StartFrame += FramesOf(Parts(PartIndex))
                PartIndex += 1
                If PartIndex = 0 Then CueLines.Add("FILE """ + Path.GetFileName(Parts(0)) + """ BINARY")
                Continue For
            End If
            If PartIndex < 0 Then Continue For
            Dim Index As Match = Regex.Match(Line, "^(\s*INDEX\s+\d+\s+)(\d+):(\d+):(\d+)\s*$", RegexOptions.IgnoreCase)
            If Index.Success Then
                Dim Frames As Long = (CLng(Index.Groups(2).Value) * 60 + CLng(Index.Groups(3).Value)) * FramesPerSecond + CLng(Index.Groups(4).Value)
                CueLines.Add(Index.Groups(1).Value + FormatFrames(Frames + StartFrame))
            Else
                CueLines.Add(Line)
            End If
        Next

        Using Output As New FileStream(OutputBin, FileMode.Create, FileAccess.Write)
            Dim Buffer(1048575) As Byte
            For Each Part As String In Parts
                Using Input As New FileStream(Part, FileMode.Open, FileAccess.Read)
                    Dim Count As Integer
                    Do
                        Count = Input.Read(Buffer, 0, Buffer.Length)
                        If Count > 0 Then Output.Write(Buffer, 0, Count)
                    Loop While Count > 0
                End Using
            Next
        End Using
        File.WriteAllLines(OutputCue, CueLines)
    End Sub

    ''' <summary>The number of sectors of a BIN file. The file must be a whole number of 2352 byte sectors.</summary>
    Private Shared Function FramesOf(BinPath As String) As Long
        Dim Length As Long = New FileInfo(BinPath).Length
        If Length Mod SectorSize <> 0 Then Throw New InvalidDataException(Localizer.F("The BIN file '{0}' is not a whole number of 2352 byte sectors, so its tracks cannot be joined.", Path.GetFileName(BinPath)))
        Return Length \ SectorSize
    End Function

    ''' <summary>Frames to the "MM:SS:FF" text of a CUE sheet.</summary>
    Private Shared Function FormatFrames(Frames As Long) As String
        Return (Frames \ FramesPerMinute).ToString("00") + ":" + ((Frames \ FramesPerSecond) Mod 60).ToString("00") + ":" + (Frames Mod FramesPerSecond).ToString("00")
    End Function

End Class
