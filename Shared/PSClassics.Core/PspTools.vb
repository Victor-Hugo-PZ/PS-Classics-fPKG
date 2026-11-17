Imports System.IO

''' <summary>Binary helpers used to replace the encrypted EBOOT.BIN inside a PSP image with its decrypted version.</summary>
Public Class PspTools

    ''' <summary>Reads up to <paramref name="Length"/> bytes from a file. The result always has <paramref name="Length"/> elements (zero filled after the end of the file).</summary>
    Public Shared Function ReadBytes(DataFile As String, Offset As Long, Length As Integer) As Byte()
        Dim Result(Length - 1) As Byte
        Using NewFileStream As New FileStream(DataFile, FileMode.Open, FileAccess.Read, FileShare.Read)
            If Offset >= NewFileStream.Length Then Return Result
            NewFileStream.Seek(Offset, SeekOrigin.Begin)
            Dim TotalRead As Integer = 0
            While TotalRead < Length
                Dim ReadCount As Integer = NewFileStream.Read(Result, TotalRead, Length - TotalRead)
                If ReadCount <= 0 Then Exit While
                TotalRead += ReadCount
            End While
        End Using
        Return Result
    End Function

    ''' <summary>Returns the offset of the first occurrence of <paramref name="Query"/> in a file, or -1 when it is not found.</summary>
    Public Shared Function FindOffset(FileName As String, Query As Byte()) As Long
        If Query Is Nothing OrElse Query.Length = 0 Then Return -1

        Using NewFileStream As New FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20)
            If Query.Length > NewFileStream.Length Then Return -1

            Dim ChunkSize As Integer = Math.Max(4 * 1024 * 1024, Query.Length * 2)
            Dim Chunk(ChunkSize - 1) As Byte
            Dim Carry As Integer = 0            'bytes kept from the previous chunk so matches spanning two chunks are found
            Dim ChunkStartOffset As Long = 0    'file offset of Chunk(0)

            Do
                Dim ReadCount As Integer = NewFileStream.Read(Chunk, Carry, ChunkSize - Carry)
                If ReadCount <= 0 Then Exit Do

                Dim Valid As Integer = Carry + ReadCount
                Dim LastStart As Integer = Valid - Query.Length
                Dim Index As Integer = 0
                While Index <= LastStart
                    Index = Array.IndexOf(Chunk, Query(0), Index, LastStart - Index + 1)
                    If Index < 0 Then Exit While
                    If MatchesAt(Chunk, Index, Query) Then Return ChunkStartOffset + Index
                    Index += 1
                End While

                Dim Keep As Integer = Math.Min(Query.Length - 1, Valid)
                Array.Copy(Chunk, Valid - Keep, Chunk, 0, Keep)
                ChunkStartOffset += Valid - Keep
                Carry = Keep
            Loop
        End Using

        Return -1
    End Function

    Private Shared Function MatchesAt(Data As Byte(), Index As Integer, Query As Byte()) As Boolean
        For I As Integer = 1 To Query.Length - 1
            If Data(Index + I) <> Query(I) Then Return False
        Next
        Return True
    End Function

    ''' <summary>Overwrites bytes of a file starting at <paramref name="Offset"/>.</summary>
    Public Shared Sub WriteBytes(FileToWrite As String, Offset As Long, Data As Byte())
        Using NewFileStream As New FileStream(FileToWrite, FileMode.Open, FileAccess.Write, FileShare.None)
            NewFileStream.Seek(Offset, SeekOrigin.Begin)
            NewFileStream.Write(Data, 0, Data.Length)
        End Using
    End Sub

    ''' <summary>
    ''' Replaces the encrypted EBOOT stored in a PSP image by its decrypted version.
    ''' Returns False (and leaves the image untouched) when the encrypted EBOOT cannot be located inside the image.
    ''' </summary>
    Public Shared Function ReplaceEbootInImage(ImageFile As String, EncryptedEbootFile As String, DecryptedEbootFile As String) As Boolean
        'Only the beginning of the EBOOT is needed to locate it inside the image
        Dim SearchLength As Integer = CInt(Math.Min(New FileInfo(EncryptedEbootFile).Length, 512320L))
        Dim SearchBytes As Byte() = ReadBytes(EncryptedEbootFile, 0, SearchLength)

        Dim OffsetValue As Long = FindOffset(ImageFile, SearchBytes)
        If OffsetValue < 0 Then Return False

        WriteBytes(ImageFile, OffsetValue, File.ReadAllBytes(DecryptedEbootFile))
        Return True
    End Function

End Class
