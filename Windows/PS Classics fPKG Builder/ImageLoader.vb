Imports System.IO
Imports System.Windows.Media.Imaging

''' <summary>Loads user images for the previews without locking the files.</summary>
Public Class ImageLoader

    ''' <summary>
    ''' Loads an image file into a frozen bitmap (safe to use from any thread). The file is read into memory, so it can
    ''' be replaced or deleted while it is displayed. Returns Nothing when the path is empty, missing or not an image.
    ''' </summary>
    Public Shared Function TryLoad(ImagePath As String, Optional DecodePixelWidth As Integer = 0) As BitmapSource
        If String.IsNullOrWhiteSpace(ImagePath) OrElse Not File.Exists(ImagePath) Then Return Nothing

        Dim Loaded As BitmapSource = Decode(File.ReadAllBytes(ImagePath), DecodePixelWidth)
        If Loaded IsNot Nothing Then Return Loaded

        'Formats the Windows decoder may not know (WebP...) are converted by the shared Core library, like the build does
        Dim Converted As Byte() = ImageConverter.ToPreviewPng(ImagePath, If(DecodePixelWidth > 0, DecodePixelWidth, 1920))
        If Converted Is Nothing Then
            Debug.WriteLine("Could not load image '" + ImagePath + "'")
            Return Nothing
        End If
        Return Decode(Converted, 0)
    End Function

    Private Shared Function Decode(Data As Byte(), DecodePixelWidth As Integer) As BitmapSource
        Try
            Using ImageStream As New MemoryStream(Data)
                Dim NewBitmap As New BitmapImage()
                NewBitmap.BeginInit()
                NewBitmap.CacheOption = BitmapCacheOption.OnLoad
                NewBitmap.StreamSource = ImageStream
                If DecodePixelWidth > 0 Then NewBitmap.DecodePixelWidth = DecodePixelWidth
                NewBitmap.EndInit()
                NewBitmap.Freeze()
                Return NewBitmap
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Describes how the build will change an image that is resized to <paramref name="TargetWidth"/> x <paramref name="TargetHeight"/>.
    ''' The build stretches images (it does not crop), so a different aspect ratio distorts them. Returns an empty string when the image fits.
    ''' </summary>
    Public Shared Function DescribeStretch(ImageName As String, Width As Integer, Height As Integer, TargetWidth As Integer, TargetHeight As Integer) As String
        Return ImageConverter.DescribeStretch(ImageName, Width, Height, TargetWidth, TargetHeight)
    End Function

End Class
