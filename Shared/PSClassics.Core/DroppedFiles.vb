Imports System.IO

''' <summary>What a file dropped on the window is.</summary>
Public Enum DroppedFileKind
    Unknown
    PS1Disc
    PS2Disc
    PSPDisc
    Icon
    Background
    TxtConfig
    LuaConfig
    MemoryCard
    Package
End Enum

''' <summary>Decides where a dropped file goes, so the user can drag the game or an image anywhere on the window.</summary>
Public Class DroppedFiles

    ''' <summary>Size of a PS2 memory card image (8 MB + ECC).</summary>
    Private Const MemoryCardMaximumBytes As Long = 9L * 1024 * 1024

    Public Shared Function Classify(FilePath As String) As DroppedFileKind
        If String.IsNullOrWhiteSpace(FilePath) OrElse Not File.Exists(FilePath) Then Return DroppedFileKind.Unknown
        Select Case Path.GetExtension(FilePath).ToLowerInvariant()
            Case ".cue"
                Return DroppedFileKind.PS1Disc
            Case ".bin"
                'A small .bin is a PS2 memory card, a big one a PS1 disc
                Return If(New FileInfo(FilePath).Length <= MemoryCardMaximumBytes AndAlso New FileInfo(FilePath).Length >= 8L * 1024 * 1024 AndAlso
                          Not File.Exists(Path.ChangeExtension(FilePath, ".cue")), DroppedFileKind.MemoryCard, DroppedFileKind.PS1Disc)
            Case ".iso"
                Return If(IsoTools.FileExistInISO(FilePath, "\PSP_GAME\PARAM.SFO"), DroppedFileKind.PSPDisc, DroppedFileKind.PS2Disc)
            Case ".txt"
                Return DroppedFileKind.TxtConfig
            Case ".lua"
                Return DroppedFileKind.LuaConfig
            Case ".vm2", ".ps2"
                Return DroppedFileKind.MemoryCard
            Case ".pkg"
                Return DroppedFileKind.Package
        End Select
        If ImageFormats.IsSupported(FilePath) Then Return ImageRole(FilePath)
        Return DroppedFileKind.Unknown
    End Function

    ''' <summary>Wide images (at least 4:3) are backgrounds, the others icons.</summary>
    Public Shared Function ImageRole(ImageFile As String) As DroppedFileKind
        Dim Size = ImageConverter.GetSize(ImageFile)
        If Size Is Nothing Then Return DroppedFileKind.Unknown
        Return If(Size.Value.Width >= Size.Value.Height * 1.3, DroppedFileKind.Background, DroppedFileKind.Icon)
    End Function

    ''' <summary>For a PS1 game the .BIN file is selected: a dropped .CUE is replaced by its first .BIN file (the one with the same name, or the first one it lists).</summary>
    Public Shared Function DiscFile(FilePath As String) As String
        If Not Path.GetExtension(FilePath).Equals(".cue", StringComparison.OrdinalIgnoreCase) Then Return FilePath
        Dim Folder As String = Path.GetDirectoryName(FilePath)
        Dim Listed As String = DiscImages.ReferencedFiles(FilePath).
            Select(Function(Name) Path.Combine(Folder, Name)).FirstOrDefault(Function(Candidate) File.Exists(Candidate))
        If Listed IsNot Nothing Then Return Listed
        Dim BaseName As String = Path.GetFileNameWithoutExtension(FilePath)
        Dim Bin As String = Directory.GetFiles(Folder).FirstOrDefault(Function(Candidate) Path.GetFileNameWithoutExtension(Candidate).Equals(BaseName, StringComparison.OrdinalIgnoreCase) AndAlso
                                                                         Path.GetExtension(Candidate).Equals(".bin", StringComparison.OrdinalIgnoreCase))
        Return If(Bin, "")
    End Function

End Class
