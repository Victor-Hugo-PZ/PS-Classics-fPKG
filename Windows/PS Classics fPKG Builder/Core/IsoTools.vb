Imports System.IO
Imports System.Text.RegularExpressions
Imports DiscUtils

''' <summary>Helpers to read game images (ISO 9660 / UDF) and the ".dat" config databases (UDF images) shipped in the Tools folder.</summary>
Public Class IsoTools

    ''' <summary>Checks if a file exists inside a ".dat" (UDF) config database.</summary>
    Public Shared Function IsConfigAvailable(GameID As String, ConfigDatabaseFile As String) As Boolean
        Try
            Using NewFileStream As New FileStream(ConfigDatabaseFile, FileMode.Open, FileAccess.Read)
                Dim NewUdfReader As New Udf.UdfReader(NewFileStream, 2048)
                Try
                    Using NewConfigStream As Stream = NewUdfReader.OpenFile(GameID, FileMode.Open)
                        Return True
                    End Using
                Catch ex As Exception
                    Return False
                End Try
            End Using
        Catch ex As Exception
            Debug.WriteLine("Config database '" + ConfigDatabaseFile + "' could not be read: " + ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>Extracts the boot ELF of a PS2 ISO into the cache folder. Returns the extracted path or an empty string.</summary>
    Public Shared Function GetELFfromISO(GameISOFile As String, GameELFName As String) As String
        Dim CacheDir As String = AppPaths.CacheDirectory
        Directory.CreateDirectory(CacheDir)

        Try
            Using NewFileStream As New FileStream(GameISOFile, FileMode.Open, FileAccess.Read)
                Dim NewCDReader As New Iso9660.CDReader(NewFileStream, True)
                'Open the source first so no empty file is created when the ELF is missing, and dispose both streams
                Using ELFSourceStream As Streams.SparseStream = NewCDReader.OpenFile(GameELFName, FileMode.Open)
                    Using ELFExtractionFileStream As New FileStream(CacheDir + "\" + GameELFName, FileMode.Create)
                        ELFSourceStream.CopyTo(ELFExtractionFileStream)
                    End Using
                End Using
            End Using
            Return CacheDir + "\" + GameELFName
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>Runs "crc.exe" on the boot ELF of a PS2 game and returns the CRC as alphanumeric text.</summary>
    Public Shared Function GetGameCRC(PS2GamePath As String) As String
        If String.IsNullOrEmpty(PS2GamePath) Then Return ""

        Dim NewProcessStartInfo As New ProcessStartInfo(AppPaths.ToolsDirectory + "\PS4\crc.exe") With {.UseShellExecute = False, .RedirectStandardOutput = True, .CreateNoWindow = True, .Arguments = """" + PS2GamePath + """"}
        Using NewProcess As New Process With {.StartInfo = NewProcessStartInfo}
            NewProcess.Start()
            Dim Output As String = NewProcess.StandardOutput.ReadToEnd()
            NewProcess.WaitForExit()
            Return Regex.Replace(Output.Replace("crc:", "").Trim(), "[^a-zA-Z0-9]", "")
        End Using
    End Function

    ''' <summary>Extracts a file from a ".dat" (UDF) config database. Returns the destination path or an empty string.</summary>
    Public Shared Function ExtractFileFromISO(GameISOFile As String, FileToExtract As String, FileDestinationPath As String) As String
        Try
            Using NewFileStream As New FileStream(GameISOFile, FileMode.Open, FileAccess.Read)
                Dim NewUdfReader As New Udf.UdfReader(NewFileStream, &H800)
                Using UdfSourceStream As Streams.SparseStream = NewUdfReader.OpenFile(FileToExtract, FileMode.Open)
                    Using NewFileStream2 As New FileStream(FileDestinationPath, FileMode.Create)
                        UdfSourceStream.CopyTo(NewFileStream2)
                    End Using
                End Using
            End Using
            Return FileDestinationPath
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>Reads a text file (config or patch) from a ".dat" (UDF) database. Returns an empty string when it is not found.</summary>
    Public Shared Function GetPNACHFromDAT(DATFile As String, FileToExtract As String) As String
        Try
            Using NewFileStream As New FileStream(DATFile, FileMode.Open, FileAccess.Read)
                Dim NewUdfReader As New Udf.UdfReader(NewFileStream, 2048)
                Using NewStreamReader As New StreamReader(NewUdfReader.OpenFile(FileToExtract, FileMode.Open))
                    Return NewStreamReader.ReadToEnd()
                End Using
            End Using
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>Checks if a file exists inside a PSP ISO (ISO 9660).</summary>
    Public Shared Function FileExistInISO(GameISOPath As String, FileToSearch As String) As Boolean
        Try
            Using NewFileStream As New FileStream(GameISOPath, FileMode.Open, FileAccess.Read)
                Dim NewIso9660CDReader As New Iso9660.CDReader(NewFileStream, True)
                Return NewIso9660CDReader.FileExists(FileToSearch)
            End Using
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>Extracts a file from a PSP ISO (ISO 9660). The destination folder is created when needed. Returns the destination path or an empty string.</summary>
    Public Shared Function ExtractFileFromPSPISO(ISOPath As String, FileName As String, DestinationPath As String) As String
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(DestinationPath))
            Using NewFileStream As New FileStream(ISOPath, FileMode.Open, FileAccess.Read)
                Dim NewIso9660CDReader As New Iso9660.CDReader(NewFileStream, True)
                Using NewSparseStream As Streams.SparseStream = NewIso9660CDReader.OpenFile(FileName, FileMode.Open)
                    Using OutputFileStream As New FileStream(DestinationPath, FileMode.Create)
                        NewSparseStream.CopyTo(OutputFileStream)
                    End Using
                End Using
            End Using
            Return DestinationPath
        Catch ex As Exception
            Return ""
        End Try
    End Function

End Class
