Imports System.IO
Imports System.IO.Compression

''' <summary>
''' Turns a PSP .cso file (compressed ISO, "CISO" format) back into a normal .iso file, so PSP games that come
''' compressed can be used directly.
''' Layout: 24 byte header ("CISO", header size, total bytes u64, block size, version, index shift), then one u32 per block
''' plus one (the position of the block, shifted left by the index shift; bit 31 = the block is stored as it is), then the blocks.
''' A block that is not marked plain is raw deflate data.
''' </summary>
Public Class CsoReader

    Private Const HeaderLength As Integer = 24

    ''' <summary>True when the file starts with the "CISO" mark.</summary>
    Public Shared Function IsCso(CsoFile As String) As Boolean
        Return StartsWith(CsoFile, "CISO")
    End Function

    ''' <summary>True for the "ZISO" format (LZ4 compressed, used by some PSP tools), which is not supported.</summary>
    Public Shared Function IsZiso(CsoFile As String) As Boolean
        Return StartsWith(CsoFile, "ZISO")
    End Function

    Private Shared Function StartsWith(FileName As String, Mark As String) As Boolean
        Try
            Using Input As New FileStream(FileName, FileMode.Open, FileAccess.Read)
                Dim Buffer(3) As Byte
                If Input.Read(Buffer, 0, 4) < 4 Then Return False
                Return Text.Encoding.ASCII.GetString(Buffer) = Mark
            End Using
        Catch ex As Exception
            Return False
        End Try
    End Function

    ''' <summary>The size of the ISO inside a CSO file (the "total bytes" of its header).</summary>
    Public Shared Function GetIsoSize(CsoFile As String) As Long
        Using Input As New FileStream(CsoFile, FileMode.Open, FileAccess.Read)
            Return ReadHeader(Input).TotalBytes
        End Using
    End Function

    Private Class CsoHeader
        Public TotalBytes As Long
        Public BlockSize As Integer
        Public IndexShift As Integer
        Public HeaderSize As Integer
    End Class

    Private Shared Function ReadHeader(Input As Stream) As CsoHeader
        Dim Data(HeaderLength - 1) As Byte
        If Input.Read(Data, 0, HeaderLength) < HeaderLength OrElse Text.Encoding.ASCII.GetString(Data, 0, 4) <> "CISO" Then
            Throw New InvalidDataException(Localizer.T("This is not a valid .cso file."))
        End If
        Dim Header As New CsoHeader With {
            .HeaderSize = CInt(BitConverter.ToUInt32(Data, 4)),
            .TotalBytes = CLng(BitConverter.ToUInt64(Data, 8)),
            .BlockSize = CInt(BitConverter.ToUInt32(Data, 16)),
            .IndexShift = Data(21)
        }
        If Header.TotalBytes <= 0 OrElse Header.BlockSize < 512 OrElse Header.BlockSize > 1048576 OrElse Header.IndexShift > 8 OrElse Header.HeaderSize < HeaderLength Then
            Throw New InvalidDataException(Localizer.T("This is not a valid .cso file."))
        End If
        Return Header
    End Function

    ''' <summary>Writes the ISO of a CSO file. <paramref name="Progress"/> receives 0 to 100.</summary>
    Public Shared Sub ConvertToIso(CsoFile As String, IsoFile As String, Optional Progress As Action(Of Integer) = Nothing)
        Using Input As New FileStream(CsoFile, FileMode.Open, FileAccess.Read)
            Dim Header As CsoHeader = ReadHeader(Input)
            Dim BlockCount As Integer = CInt((Header.TotalBytes + Header.BlockSize - 1) \ Header.BlockSize)

            Input.Position = Header.HeaderSize
            Dim IndexBytes((BlockCount + 1) * 4 - 1) As Byte
            If Input.Read(IndexBytes, 0, IndexBytes.Length) < IndexBytes.Length Then
                Throw New InvalidDataException(Localizer.T("The .cso file is cut short: its block index is incomplete."))
            End If
            Dim Index(BlockCount) As UInteger
            For Position As Integer = 0 To BlockCount
                Index(Position) = BitConverter.ToUInt32(IndexBytes, Position * 4)
            Next

            Dim Block(Header.BlockSize - 1) As Byte
            Dim Stored As Byte() = New Byte(Header.BlockSize * 2) {}
            Dim Written As Long = 0
            Dim LastPercent As Integer = -1
            Using Output As New FileStream(IsoFile, FileMode.Create, FileAccess.Write)
                For BlockNumber As Integer = 0 To BlockCount - 1
                    Dim IsPlain As Boolean = (Index(BlockNumber) And &H80000000UI) <> 0
                    Dim Start As Long = CLng(Index(BlockNumber) And &H7FFFFFFFUI) << Header.IndexShift
                    Dim [End] As Long = CLng(Index(BlockNumber + 1) And &H7FFFFFFFUI) << Header.IndexShift
                    'The end of the last block may point into padding that the file does not keep
                    If BlockNumber = BlockCount - 1 AndAlso [End] > Input.Length AndAlso Start <= Input.Length Then [End] = Input.Length
                    Dim Size As Integer = CInt([End] - Start)
                    If Size < 0 OrElse Start + Size > Input.Length Then
                        Throw New InvalidDataException(Localizer.T("The .cso file is damaged."))
                    End If
                    If Size > Stored.Length Then Stored = New Byte(Size) {}

                    Input.Position = Start
                    ReadExactly(Input, Stored, Size)

                    Dim Length As Integer
                    If IsPlain Then
                        Length = Math.Min(Size, Header.BlockSize)
                        Buffer.BlockCopy(Stored, 0, Block, 0, Length)
                    Else
                        Using Compressed As New MemoryStream(Stored, 0, Size)
                            Using Inflater As New DeflateStream(Compressed, CompressionMode.Decompress)
                                Length = 0
                                Dim Count As Integer
                                Do
                                    Count = Inflater.Read(Block, Length, Header.BlockSize - Length)
                                    Length += Count
                                Loop While Count > 0 AndAlso Length < Header.BlockSize
                            End Using
                        End Using
                    End If

                    Dim Remaining As Long = Header.TotalBytes - Written
                    Dim ToWrite As Integer = CInt(Math.Min(CLng(Length), Remaining))
                    If ToWrite < Math.Min(CLng(Header.BlockSize), Remaining) Then
                        Throw New InvalidDataException(Localizer.T("The .cso file is damaged."))
                    End If
                    Output.Write(Block, 0, ToWrite)
                    Written += ToWrite

                    If Progress IsNot Nothing Then
                        Dim Percent As Integer = CInt(Written * 100 \ Header.TotalBytes)
                        If Percent <> LastPercent Then
                            LastPercent = Percent
                            Progress(Percent)
                        End If
                    End If
                Next
            End Using
        End Using
    End Sub

    Private Shared Sub ReadExactly(Input As Stream, Buffer As Byte(), Count As Integer)
        Dim Done As Integer = 0
        While Done < Count
            Dim Read As Integer = Input.Read(Buffer, Done, Count - Done)
            If Read <= 0 Then Throw New InvalidDataException(Localizer.T("The .cso file is cut short."))
            Done += Read
        End While
    End Sub

    ''' <summary>Folder where converted ISO files are kept.</summary>
    Public Shared ReadOnly Property ConvertedDirectory As String
        Get
            Return Path.Combine(AppPaths.CacheDirectory, "Converted")
        End Get
    End Property

    ''' <summary>
    ''' The ISO of a CSO file, made in <see cref="ConvertedDirectory"/> (an earlier conversion of the same, unchanged file is reused).
    ''' Returns the path of the .iso file.
    ''' </summary>
    Public Shared Function ConvertToCache(CsoFile As String, Optional Progress As Action(Of Integer) = Nothing) As String
        If IsZiso(CsoFile) Then Throw New NotSupportedException(Localizer.T("This .cso file uses the ZSO (LZ4) format, which is not supported. Convert it to .iso or a normal .cso first."))
        Dim Expected As Long = GetIsoSize(CsoFile)

        Directory.CreateDirectory(ConvertedDirectory)
        Dim IsoFile As String = Path.Combine(ConvertedDirectory, Path.GetFileNameWithoutExtension(CsoFile) + ".iso")
        If File.Exists(IsoFile) AndAlso New FileInfo(IsoFile).Length = Expected AndAlso File.GetLastWriteTimeUtc(IsoFile) >= File.GetLastWriteTimeUtc(CsoFile) Then
            Return IsoFile
        End If

        Dim PartialFile As String = IsoFile + ".part"
        Try
            ConvertToIso(CsoFile, PartialFile, Progress)
            If File.Exists(IsoFile) Then File.Delete(IsoFile)
            File.Move(PartialFile, IsoFile)
        Finally
            If File.Exists(PartialFile) Then File.Delete(PartialFile)
        End Try
        Return IsoFile
    End Function

End Class
