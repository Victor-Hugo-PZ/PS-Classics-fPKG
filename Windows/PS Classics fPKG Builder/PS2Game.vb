Imports System.IO

Public Class PS2Game

    Public Shared Function GetPS2GameID(GameISO As String) As String
        Dim GameID As String = ""

        Using SevenZip As New Process()
            SevenZip.StartInfo.FileName = AppPaths.BaseDirectory + "\Tools\7z.exe"
            SevenZip.StartInfo.Arguments = "l -ba """ + GameISO + """"
            SevenZip.StartInfo.RedirectStandardOutput = True
            SevenZip.StartInfo.UseShellExecute = False
            SevenZip.StartInfo.CreateNoWindow = True
            SevenZip.Start()

            'Read the output
            Dim OutputReader As StreamReader = SevenZip.StandardOutput
            Dim ProcessOutput As String() = OutputReader.ReadToEnd().Split(New String() {vbCrLf}, StringSplitOptions.None)

            If ProcessOutput.Length > 0 Then
                For Each Line As String In ProcessOutput
                    If Line.Contains("SLES_") Or Line.Contains("SLUS_") Or Line.Contains("SCES_") Or Line.Contains("SCUS_") Then
                        If Line.Contains("Volume:") Then 'ID found in the ISO Header
                            Dim VolumeParts As String() = Line.Split(New String() {"Volume: "}, StringSplitOptions.RemoveEmptyEntries)
                            If VolumeParts.Length > 1 Then
                                GameID = VolumeParts(1)
                                Exit For
                            End If
                        Else 'ID found in the ISO files
                            Dim LineParts As String() = Line.Split(New Char() {}, StringSplitOptions.RemoveEmptyEntries)
                            If LineParts.Length > 5 Then
                                GameID = LineParts(5).Trim()
                                Exit For
                            End If
                        End If
                    End If
                Next
            End If

        End Using

        Return GameID
    End Function

    Public Shared Function GetPS2GameTitleFromDatabaseList(GameID As String) As String
        Dim FoundGameTitle As String = ""
        GameID = GameID.Replace("-", "")

        For Each GameTitle As String In File.ReadLines(AppPaths.BaseDirectory + "\Tools\ps2ids.txt")
            If GameTitle.Contains(GameID) Then
                FoundGameTitle = GameTitle.Split(";"c)(1)
                Exit For
            End If
        Next

        If String.IsNullOrEmpty(FoundGameTitle) Then
            Return "Unknown PS2 game"
        Else
            Return FoundGameTitle
        End If
    End Function

End Class
