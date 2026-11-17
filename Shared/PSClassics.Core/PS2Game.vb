Imports System.IO

Public Class PS2Game

    ''' <summary>
    ''' Extracts the game ID from the listing printed by "7z l -ba game.iso".
    ''' The ID is either found in the volume header ("Volume: SLUS_123.45") or as the name of the boot ELF file in the root.
    ''' Returns an empty string when no ID is found.
    ''' </summary>
    Public Shared Function ParseGameIDFromListing(ListingLines As IEnumerable(Of String)) As String
        For Each Line As String In ListingLines
            If Line.Contains("SLES_") Or Line.Contains("SLUS_") Or Line.Contains("SCES_") Or Line.Contains("SCUS_") Then
                If Line.Contains("Volume:") Then 'ID found in the ISO Header
                    'Everything after "Volume: " (the original split-based code failed when the line started with "Volume: ")
                    Dim VolumeIndex As Integer = Line.IndexOf("Volume: ")
                    If VolumeIndex >= 0 Then
                        Dim VolumeID As String = Line.Substring(VolumeIndex + "Volume: ".Length).Trim()
                        If VolumeID.Length > 0 Then
                            Return VolumeID
                        End If
                    End If
                Else 'ID found in the ISO files
                    Dim LineParts As String() = Line.Split(New Char() {}, StringSplitOptions.RemoveEmptyEntries)
                    If LineParts.Length > 5 Then
                        Return LineParts(5).Trim()
                    End If
                End If
            End If
        Next
        Return ""
    End Function

    ''' <summary>Builds the "SLUS_212.58" style game ID from a 9 character NP title ("SLUS21258"). Returns an empty string for any other length.</summary>
    Public Shared Function FormatGameIDFromNPTitle(NPTitle As String) As String
        If NPTitle Is Nothing OrElse NPTitle.Length <> 9 Then Return ""
        Return NPTitle.Insert(4, "_").Insert(8, ".")
    End Function

    ''' <summary>
    ''' Reads the game ID of a PS2 ISO: the name of the boot ELF in the root of the disc ("SLUS_212.58").
    ''' Uses the ISO 9660 reader directly (the original app listed the ISO with 7-Zip). Returns an empty string when no ID is found.
    ''' </summary>
    Public Shared Function GetPS2GameID(GameISO As String) As String
        Try
            Using IsoStream As FileStream = File.OpenRead(GameISO)
                Dim Reader As New DiscUtils.Iso9660.CDReader(IsoStream, True)
                Return FindGameIDInFileNames(Reader.GetFiles("\").Select(Function(Name) Path.GetFileName(Name)))
            End Using
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>The first root file name that looks like a PS2 boot ELF ("SLUS_212.58"), without the ISO version suffix (";1").</summary>
    Public Shared Function FindGameIDInFileNames(FileNames As IEnumerable(Of String)) As String
        For Each Name As String In FileNames
            Dim Clean As String = Name.Split(";"c)(0).Trim()
            If Clean.Contains("SLES_") Or Clean.Contains("SLUS_") Or Clean.Contains("SCES_") Or Clean.Contains("SCUS_") Then
                Return Clean
            End If
        Next
        Return ""
    End Function

    Public Shared Function GetPS2GameTitleFromDatabaseList(GameID As String) As String
        Return GetPS2GameTitleFromDatabaseList(GameID, Path.Combine(AppPaths.ToolsDirectory, "ps2ids.txt"))
    End Function

    ''' <summary>Looks up the title of a game in a "ID;Title" database file. Returns "Unknown PS2 game" when the game is not listed.</summary>
    Public Shared Function GetPS2GameTitleFromDatabaseList(GameID As String, DatabaseFile As String) As String
        Dim FoundGameTitle As String = ""
        GameID = GameID.Replace("-", "")
        If String.IsNullOrEmpty(GameID) Then Return "Unknown PS2 game"

        For Each GameTitle As String In File.ReadLines(DatabaseFile)
            If GameTitle.Contains(GameID) Then
                Dim Parts As String() = GameTitle.Split(";"c)
                If Parts.Length > 1 Then
                    FoundGameTitle = Parts(1)
                    Exit For
                End If
            End If
        Next

        If String.IsNullOrEmpty(FoundGameTitle) Then
            Return "Unknown PS2 game"
        Else
            Return FoundGameTitle
        End If
    End Function

End Class
