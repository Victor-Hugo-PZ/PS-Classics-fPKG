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

        Try
            Using ImageStream As New MemoryStream(File.ReadAllBytes(ImagePath))
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
            Debug.WriteLine("Could not load image '" + ImagePath + "': " + ex.Message)
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

End Class
