''' <summary>
''' Image formats accepted for the icon and the background. Every image is decoded and converted to a 24 bpp PNG
''' by the build (see BuilderHelpers.SavePNG), so the user does not need to convert the file first.
''' </summary>
Public Class ImageFormats

    ''' <summary>Extensions (without dot) accepted in the file dialogs.</summary>
    Public Shared ReadOnly Extensions As String() = {"png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff"}

    ''' <summary>Filter for OpenFileDialog: all supported images first, then one entry per format.</summary>
    Public Shared Function OpenFilter() As String
        Dim Patterns As String = String.Join(";", Extensions.Select(Function(Ext) "*." + Ext))
        Dim Filter As String = "Images (" + Patterns + ")|" + Patterns
        Filter += "|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|BMP (*.bmp)|*.bmp|GIF (*.gif)|*.gif|TIFF (*.tif;*.tiff)|*.tif;*.tiff"
        Return Filter
    End Function

    ''' <summary>True when the file name has one of the supported image extensions (case insensitive).</summary>
    Public Shared Function IsSupported(FileName As String) As Boolean
        If String.IsNullOrWhiteSpace(FileName) Then Return False
        Dim Ext As String = System.IO.Path.GetExtension(FileName).TrimStart("."c).ToLowerInvariant()
        Return Extensions.Contains(Ext)
    End Function

End Class
