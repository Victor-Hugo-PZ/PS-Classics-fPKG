Imports System.IO
Imports System.IO.Compression
Imports SkiaSharp

''' <summary>
''' Converts the icon and background chosen by the user into the PNG files of a PS4 package (24 bit RGB, exact size).
''' Uses SkiaSharp, so Windows, Linux and macOS produce the same images.
''' </summary>
Public Class ImageConverter

    ''' <summary>Reads the size of an image without decoding all of it. Returns Nothing when the file is not a supported image.</summary>
    Public Shared Function GetSize(ImageFile As String) As (Width As Integer, Height As Integer)?
        Try
            Using Codec As SKCodec = SKCodec.Create(ImageFile)
                If Codec Is Nothing Then Return Nothing
                Return (Codec.Info.Width, Codec.Info.Height)
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Decodes any supported image (PNG, JPEG, BMP, GIF, WebP), stretches it to <paramref name="Width"/> x <paramref name="Height"/>
    ''' (like the original app, without cropping) and returns it as 24 bit PNG bytes. Transparent parts become black.
    ''' </summary>
    Public Shared Function ToPng24(ImageFile As String, Width As Integer, Height As Integer) As Byte()
        Using Source As SKBitmap = SKBitmap.Decode(ImageFile)
            If Source Is Nothing Then
                Throw New InvalidDataException("The image could not be read: " + Path.GetFileName(ImageFile) + ". Supported formats: " + String.Join(", ", ImageFormats.Extensions.Select(Function(Ext) Ext.ToUpperInvariant())) + ".")
            End If

            Dim TargetInfo As New SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul)
            Using Resized As SKBitmap = Source.Resize(TargetInfo, New SKSamplingOptions(SKCubicResampler.Mitchell))
                If Resized Is Nothing Then Throw New InvalidDataException("The image could not be resized: " + Path.GetFileName(ImageFile))

                'Premultiplied RGB is the color over a black background
                Dim Rgba As Byte() = Resized.Bytes
                Dim Rgb(Width * Height * 3 - 1) As Byte
                Dim Target As Integer = 0
                For Source4 As Integer = 0 To Rgba.Length - 1 Step 4
                    Rgb(Target) = Rgba(Source4)
                    Rgb(Target + 1) = Rgba(Source4 + 1)
                    Rgb(Target + 2) = Rgba(Source4 + 2)
                    Target += 3
                Next
                Return PngWriter.EncodeRgb24(Rgb, Width, Height)
            End Using
        End Using
    End Function

    ''' <summary>
    ''' PNG version of any supported image for previews: keeps the aspect ratio and is at most <paramref name="MaximumWidth"/> wide.
    ''' Lets every app show formats its own UI toolkit cannot decode (for example WebP on WPF). Returns Nothing when the file is not a supported image.
    ''' </summary>
    Public Shared Function ToPreviewPng(ImageFile As String, MaximumWidth As Integer) As Byte()
        Try
            Dim Size = GetSize(ImageFile)
            If Size Is Nothing Then Return Nothing
            Dim Width As Integer = Math.Max(1, Math.Min(MaximumWidth, Size.Value.Width))
            Dim Height As Integer = Math.Max(1, CInt(Math.Round(Size.Value.Height * (Width / CDbl(Size.Value.Width)))))
            Return ToPng24(ImageFile, Width, Height)
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Describes how the build will change an image that is resized to <paramref name="TargetWidth"/> x <paramref name="TargetHeight"/>.
    ''' The build stretches images (it does not crop), so a different aspect ratio distorts them. Returns an empty string when the image fits.
    ''' </summary>
    Public Shared Function DescribeStretch(ImageName As String, Width As Integer, Height As Integer, TargetWidth As Integer, TargetHeight As Integer) As String
        If Width <= 0 OrElse Height <= 0 Then Return ""

        Dim Ratio As Double = Width / Height
        Dim TargetRatio As Double = TargetWidth / TargetHeight
        If Math.Abs(Ratio - TargetRatio) / TargetRatio > 0.03 Then
            Return ImageName + " is " + Width.ToString() + "×" + Height.ToString() + ": it will be stretched to " + TargetWidth.ToString() + "×" + TargetHeight.ToString() + "."
        End If
        If Width < TargetWidth OrElse Height < TargetHeight Then
            Return ImageName + " is " + Width.ToString() + "×" + Height.ToString() + ": it will be upscaled to " + TargetWidth.ToString() + "×" + TargetHeight.ToString() + " and may look blurry."
        End If
        Return ""
    End Function

    ''' <summary>Hints shown under the home screen preview for the selected icon and background (empty when nothing to say).</summary>
    Public Shared Function PreviewHints(IconFile As String, BackgroundFile As String) As List(Of String)
        Dim Hints As New List(Of String)
        AddHint(Hints, "The icon", IconFile, 512, 512)
        AddHint(Hints, "The background", BackgroundFile, 1920, 1080)
        Return Hints
    End Function

    Private Shared Sub AddHint(Hints As List(Of String), Name As String, ImageFile As String, TargetWidth As Integer, TargetHeight As Integer)
        If String.IsNullOrWhiteSpace(ImageFile) Then Return
        Dim Size = GetSize(ImageFile)
        If Size Is Nothing Then
            Hints.Add(Name.Substring(0, 1).ToUpperInvariant() + Name.Substring(1) + " could not be read.")
            Return
        End If
        Dim Hint As String = DescribeStretch(Name, Size.Value.Width, Size.Value.Height, TargetWidth, TargetHeight)
        If Hint <> "" Then Hints.Add(Hint)
    End Sub

    ''' <summary>Converts an image and writes it to <paramref name="DestinationFile"/>.</summary>
    Public Shared Sub SavePng24(ImageFile As String, DestinationFile As String, Width As Integer, Height As Integer)
        File.WriteAllBytes(DestinationFile, ToPng24(ImageFile, Width, Height))
    End Sub

End Class

''' <summary>Minimal PNG encoder for 8 bit RGB images (color type 2), the format the PS4 publishing tools expect.</summary>
Public Class PngWriter

    Private Shared ReadOnly CrcTable As UInteger() = BuildCrcTable()

    Public Shared Function EncodeRgb24(Rgb As Byte(), Width As Integer, Height As Integer) As Byte()
        If Rgb.Length <> Width * Height * 3 Then Throw New ArgumentException("The pixel buffer does not match the image size.")

        Using Output As New MemoryStream()
            Output.Write({&H89, &H50, &H4E, &H47, &HD, &HA, &H1A, &HA}, 0, 8)

            Dim Header(12) As Byte
            WriteBigEndian(Header, 0, CUInt(Width))
            WriteBigEndian(Header, 4, CUInt(Height))
            Header(8) = 8    'bit depth
            Header(9) = 2    'color type: RGB
            Header(10) = 0   'compression
            Header(11) = 0   'filter
            Header(12) = 0   'no interlace
            WriteChunk(Output, "IHDR", Header)

            Using Compressed As New MemoryStream()
                Using Zlib As New ZLibStream(Compressed, CompressionLevel.Optimal, True)
                    Dim RowLength As Integer = Width * 3
                    For Row As Integer = 0 To Height - 1
                        Zlib.WriteByte(0) 'filter: none
                        Zlib.Write(Rgb, Row * RowLength, RowLength)
                    Next
                End Using
                WriteChunk(Output, "IDAT", Compressed.ToArray())
            End Using

            WriteChunk(Output, "IEND", Array.Empty(Of Byte)())
            Return Output.ToArray()
        End Using
    End Function

    Private Shared Sub WriteChunk(Output As Stream, ChunkType As String, Data As Byte())
        Dim Length(3) As Byte
        WriteBigEndian(Length, 0, CUInt(Data.Length))
        Output.Write(Length, 0, 4)

        Dim TypeBytes As Byte() = Text.Encoding.ASCII.GetBytes(ChunkType)
        Output.Write(TypeBytes, 0, 4)
        Output.Write(Data, 0, Data.Length)

        Dim Crc As UInteger = &HFFFFFFFFUI
        Crc = UpdateCrc(Crc, TypeBytes)
        Crc = UpdateCrc(Crc, Data)
        Dim CrcBytes(3) As Byte
        WriteBigEndian(CrcBytes, 0, Crc Xor &HFFFFFFFFUI)
        Output.Write(CrcBytes, 0, 4)
    End Sub

    Private Shared Function UpdateCrc(Crc As UInteger, Data As Byte()) As UInteger
        For Each Value As Byte In Data
            Crc = CrcTable(CInt((Crc Xor Value) And &HFFUI)) Xor (Crc >> 8)
        Next
        Return Crc
    End Function

    Private Shared Function BuildCrcTable() As UInteger()
        Dim Table(255) As UInteger
        For N As Integer = 0 To 255
            Dim C As UInteger = CUInt(N)
            For K As Integer = 0 To 7
                C = If((C And 1UI) <> 0, &HEDB88320UI Xor (C >> 1), C >> 1)
            Next
            Table(N) = C
        Next
        Return Table
    End Function

    Private Shared Sub WriteBigEndian(Buffer As Byte(), Offset As Integer, Value As UInteger)
        Buffer(Offset) = CByte((Value >> 24) And &HFFUI)
        Buffer(Offset + 1) = CByte((Value >> 16) And &HFFUI)
        Buffer(Offset + 2) = CByte((Value >> 8) And &HFFUI)
        Buffer(Offset + 3) = CByte(Value And &HFFUI)
    End Sub

End Class
