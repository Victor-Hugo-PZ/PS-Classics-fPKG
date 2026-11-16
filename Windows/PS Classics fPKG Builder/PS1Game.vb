Imports System.IO

Public Class PS1Game

    ''' <summary>
    ''' Searches the "strings" output of a PS1 BIN file for the SYSTEM.CNF "BOOT" entry and returns the normalized game ID (for example "SLUS-00594").
    ''' Returns an empty string when no BOOT entry exists.
    ''' </summary>
    Public Shared Function FindGameID(OutputLines As IEnumerable(Of String)) As String
        For Each OutputLine As String In OutputLines
            If OutputLine.Contains("BOOT =") Or OutputLine.Contains("BOOT=") Then
                Dim GameID As String = OutputLine.Replace("BOOT = cdrom:\", "").Replace("BOOT=cdrom:\", "").Replace("BOOT = cdrom:", "").Replace(";1", "").Replace("_", "-").Replace(".", "").Replace("MGS\", "").Trim()
                Return UCase(GameID).Trim()
            End If
        Next
        Return ""
    End Function

    ''' <summary>Reads the game ID from the first 7 MB of a PS1 BIN file using "strings.exe". Returns an empty string when no ID is found.</summary>
    Public Shared Function GetPS1GameID(BinFile As String) As String
        Dim ProcessOutput As String()

        Using StringsProcess As New Process()
            StringsProcess.StartInfo.FileName = AppPaths.ToolsDirectory + "\strings.exe"
            StringsProcess.StartInfo.Arguments = "/accepteula -nobanner -b 7340032 """ + BinFile + """"
            StringsProcess.StartInfo.RedirectStandardOutput = True
            StringsProcess.StartInfo.UseShellExecute = False
            StringsProcess.StartInfo.CreateNoWindow = True
            StringsProcess.Start()

            'Read the redirected output before waiting for the exit, otherwise a full pipe buffer blocks the child process forever
            ProcessOutput = StringsProcess.StandardOutput.ReadToEnd().Split(New String() {vbCrLf}, StringSplitOptions.RemoveEmptyEntries)
            StringsProcess.WaitForExit()
        End Using

        Return FindGameID(ProcessOutput)
    End Function

    Public Shared Function GetPS1GameTitleFromDatabaseList(GameID As String) As String
        Return GetPS1GameTitleFromDatabaseList(GameID, AppPaths.ToolsDirectory + "\ps1ids.txt")
    End Function

    ''' <summary>Looks up the title of a game in a "ID;Title" database file. Returns an empty string when the game is unknown.</summary>
    Public Shared Function GetPS1GameTitleFromDatabaseList(GameID As String, DatabaseFile As String) As String
        If String.IsNullOrEmpty(GameID) Then Return ""

        For Each GameTitle As String In File.ReadLines(DatabaseFile)
            If GameTitle.Contains(GameID) Then
                Dim Parts As String() = GameTitle.Split(";"c)
                If Parts.Length > 1 Then
                    Return Parts(1)
                End If
            End If
        Next
        Return ""
    End Function

    Public Shared Function IsGameProtected(GameID As String) As String
        Return IsGameProtected(GameID, AppPaths.ToolsDirectory + "\libcrypt.txt")
    End Function

    ''' <summary>Returns the libcrypt value of a protected game ("SLES_012.26 03E7" lists), or an empty string when the game is not in the list.</summary>
    Public Shared Function IsGameProtected(GameID As String, DatabaseFile As String) As String
        If String.IsNullOrEmpty(GameID) Then Return ""

        For Each GameIDInFile As String In File.ReadLines(DatabaseFile)
            If GameIDInFile.Contains(GameID) Then
                Dim Parts As String() = GameIDInFile.Split(" "c)
                If Parts.Length > 1 Then
                    Return Parts(1)
                End If
            End If
        Next
        Return ""
    End Function

    ''' <summary>
    ''' Builds the "SLES_012.26" style ID used by the libcrypt list.
    ''' Uses the detected game ID ("SLES-01226") when there is one, otherwise the 9 character NP title ("SLES01226").
    ''' </summary>
    Public Shared Function FormatLibcryptID(DetectedGameID As String, NPTitle As String) As String
        If String.IsNullOrEmpty(DetectedGameID) Then
            If NPTitle.Length < 9 Then Return NPTitle
            Return NPTitle.Insert(4, "_").Insert(8, ".")
        End If

        Dim Formatted As String = DetectedGameID.Replace("-", "_")
        If Formatted.Length < 8 Then Return Formatted
        Return Formatted.Insert(8, ".")
    End Function

End Class
