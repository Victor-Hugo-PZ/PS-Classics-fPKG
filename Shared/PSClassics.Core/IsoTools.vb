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
                    Using ELFExtractionFileStream As New FileStream(Path.Combine(CacheDir, GameELFName), FileMode.Create)
                        ELFSourceStream.CopyTo(ELFExtractionFileStream)
                    End Using
                End Using
            End Using
            Return Path.Combine(CacheDir, GameELFName)
        Catch ex As Exception
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' CRC of the boot ELF of a PS2 game, as used by the widescreen patch database ("973793E8"): the XOR of all the
    ''' little endian 32 bit words of the file (trailing bytes that do not fill a word are ignored).
    ''' Gives the same result as the original "crc.exe" without running it. Returns an empty string for a missing file.
    ''' </summary>
    Public Shared Function GetGameCRC(PS2GamePath As String) As String
        If String.IsNullOrEmpty(PS2GamePath) OrElse Not File.Exists(PS2GamePath) Then Return ""

        Dim Crc As UInteger = 0
        Dim Buffer(1048575) As Byte
        Dim Carry(3) As Byte
        Dim CarryCount As Integer = 0
        Using Input As FileStream = File.OpenRead(PS2GamePath)
            Do
                Dim ReadCount As Integer = Input.Read(Buffer, 0, Buffer.Length)
                If ReadCount <= 0 Then Exit Do
                Dim Index As Integer = 0
                'Complete a word that was split between two reads
                While CarryCount > 0 AndAlso CarryCount < 4 AndAlso Index < ReadCount
                    Carry(CarryCount) = Buffer(Index)
                    CarryCount += 1
                    Index += 1
                End While
                If CarryCount = 4 Then
                    Crc = Crc Xor BitConverter.ToUInt32(Carry, 0)
                    CarryCount = 0
                End If
                While Index + 4 <= ReadCount
                    Crc = Crc Xor BitConverter.ToUInt32(Buffer, Index)
                    Index += 4
                End While
                While Index < ReadCount
                    Carry(CarryCount) = Buffer(Index)
                    CarryCount += 1
                    Index += 1
                End While
            Loop
        End Using
        Return Crc.ToString("X8")
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

    ''' <summary>The game title stored in the PARAM.SFO of a PSP ISO (\PSP_GAME\PARAM.SFO), or an empty string.</summary>
    Public Shared Function GetPSPTitle(ISOPath As String) As String
        Try
            Using NewFileStream As New FileStream(ISOPath, FileMode.Open, FileAccess.Read)
                Dim NewIso9660CDReader As New Iso9660.CDReader(NewFileStream, True)
                If Not NewIso9660CDReader.FileExists("\PSP_GAME\PARAM.SFO") Then Return ""
                Using NewSparseStream As Streams.SparseStream = NewIso9660CDReader.OpenFile("\PSP_GAME\PARAM.SFO", FileMode.Open)
                    Using Memory As New MemoryStream()
                        NewSparseStream.CopyTo(Memory)
                        Return SfoReader.ReadTitle(Memory.ToArray())
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Return ""
        End Try
    End Function

End Class
