''' <summary>
''' Image formats accepted for the icon and the background. Every image is decoded and converted to a 24 bit PNG
''' by the build (see <see cref="ImageConverter"/>), so the user does not need to convert the file first.
''' The list is the same on Windows, Linux and macOS (decoded with SkiaSharp).
''' </summary>
Public Class ImageFormats

    ''' <summary>Extensions (without dot) accepted in the file dialogs.</summary>
    Public Shared ReadOnly Extensions As String() = {"png", "jpg", "jpeg", "bmp", "gif", "webp"}

    ''' <summary>Filter for the Windows OpenFileDialog: all supported images first, then one entry per format.</summary>
    Public Shared Function OpenFilter() As String
        Dim Patterns As String = String.Join(";", Extensions.Select(Function(Ext) "*." + Ext))
        Dim Filter As String = "Images (" + Patterns + ")|" + Patterns
        Filter += "|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP (*.bmp)|*.bmp|GIF (*.gif)|*.gif|WebP (*.webp)|*.webp"
        Return Filter
    End Function

    ''' <summary>Glob patterns ("*.png", ...) for file pickers that take a list of patterns.</summary>
    Public Shared Function Patterns() As String()
        Return Extensions.Select(Function(Ext) "*." + Ext).ToArray()
    End Function

    ''' <summary>True when the file name has one of the supported image extensions (case insensitive).</summary>
    Public Shared Function IsSupported(FileName As String) As Boolean
        If String.IsNullOrWhiteSpace(FileName) Then Return False
        Dim Ext As String = System.IO.Path.GetExtension(FileName).TrimStart("."c).ToLowerInvariant()
        Return Extensions.Contains(Ext)
    End Function

End Class
