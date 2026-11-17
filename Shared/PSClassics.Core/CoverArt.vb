Imports System.IO
Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports SkiaSharp

''' <summary>
''' Ready-made icon and background for a game:
''' PS1 and PS2 covers are downloaded by game ID from the community cover collections used by emulators
''' (github.com/xlenore/psx-covers and ps2-covers); PSP games carry their own ICON0.PNG / PIC1.PNG on the disc.
''' The images are turned into a square icon (cover over a blurred copy of itself) and a 16:9 background.
''' </summary>
Public Class CoverArt

    Public Const IconSize As Integer = 512
    Public Const BackgroundWidth As Integer = 1920
    Public Const BackgroundHeight As Integer = 1080

    ''' <summary>Folder where the generated images are kept (per user).</summary>
    Public Shared ReadOnly Property CoversDirectory As String
        Get
            Return Path.Combine(AppPaths.DataDirectory, "covers")
        End Get
    End Property

    ''' <summary>
    ''' Turns a game ID or NP title into the serial used by the cover collections:
    ''' "SLUS_212.58", "SLUS-21258" or "SLUS21258" -> "SLUS-21258". Returns an empty string when it is not a game ID.
    ''' </summary>
    Public Shared Function NormalizeSerial(GameIdOrNPTitle As String) As String
        If String.IsNullOrWhiteSpace(GameIdOrNPTitle) Then Return ""
        Dim Compact As String = Regex.Replace(GameIdOrNPTitle.ToUpperInvariant(), "[^A-Z0-9]", "")
        Dim Match As Match = Regex.Match(Compact, "^([A-Z]{4})([0-9]{5})$")
        Return If(Match.Success, Match.Groups(1).Value + "-" + Match.Groups(2).Value, "")
    End Function

    ''' <summary>Address of the cover of a PS1 or PS2 game, or an empty string for other platforms.</summary>
    Public Shared Function CoverUrl(PlatformName As String, Serial As String) As String
        Select Case PlatformName
            Case "PS1"
                Return "https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/" + Serial + ".jpg"
            Case "PS2"
                Return "https://raw.githubusercontent.com/xlenore/ps2-covers/main/covers/default/" + Serial + ".jpg"
            Case Else
                Return ""
        End Select
    End Function

    ''' <summary>Downloads a cover. Returns Nothing when the collection has no cover for this game. Throws on network errors.</summary>
    Public Shared Async Function DownloadCoverAsync(PlatformName As String, Serial As String, Optional Client As HttpClient = Nothing) As Task(Of Byte())
        Dim Url As String = CoverUrl(PlatformName, Serial)
        If Url = "" OrElse Serial = "" Then Return Nothing
        Dim OwnClient As Boolean = Client Is Nothing
        If OwnClient Then Client = CreateClient()
        Try
            Using Response As HttpResponseMessage = Await Client.GetAsync(Url).ConfigureAwait(False)
                If Response.StatusCode = Net.HttpStatusCode.NotFound Then Return Nothing
                Response.EnsureSuccessStatusCode()
                Return Await Response.Content.ReadAsByteArrayAsync().ConfigureAwait(False)
            End Using
        Finally
            If OwnClient Then Client.Dispose()
        End Try
    End Function

    Friend Shared Function CreateClient() As HttpClient
        Dim Client As New HttpClient With {.Timeout = TimeSpan.FromSeconds(20)}
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("PSClassicsfPKGBuilder")
        Return Client
    End Function

    ''' <summary>Result of <see cref="CreateImages"/>: paths of the generated PNG files.</summary>
    Public Class GeneratedImages
        Public Property IconFile As String = ""
        Public Property BackgroundFile As String = ""
    End Class

    ''' <summary>
    ''' Creates the icon (512x512) and the background (1920x1080) from a cover or disc image, in <see cref="CoversDirectory"/>.
    ''' <paramref name="BackgroundImage"/> is used for the background when given (PSP PIC1.PNG), else the cover.
    ''' </summary>
    Public Shared Function CreateImages(CoverImage As Byte(), Name As String, Optional BackgroundImage As Byte() = Nothing, Optional TargetDirectory As String = Nothing) As GeneratedImages
        Dim Folder As String = If(TargetDirectory, CoversDirectory)
        Directory.CreateDirectory(Folder)
        Dim SafeName As String = Regex.Replace(Name, "[^A-Za-z0-9_-]", "_")
        Dim Result As New GeneratedImages With {
            .IconFile = Path.Combine(Folder, SafeName + "-icon.png"),
            .BackgroundFile = Path.Combine(Folder, SafeName + "-background.png")
        }

        Using Cover As SKBitmap = Decode(CoverImage)
            File.WriteAllBytes(Result.IconFile, Compose(Cover, IconSize, IconSize, 0.92, 0.55))
            If BackgroundImage IsNot Nothing Then
                Using Background As SKBitmap = Decode(BackgroundImage)
                    'A real background (PSP PIC1): fill the screen with it
                    File.WriteAllBytes(Result.BackgroundFile, Compose(Background, BackgroundWidth, BackgroundHeight, 0, 1))
                End Using
            Else
                File.WriteAllBytes(Result.BackgroundFile, Compose(Cover, BackgroundWidth, BackgroundHeight, 0, 0.45))
            End If
        End Using
        Return Result
    End Function

    ''' <summary>
    ''' Reads ICON0.PNG and PIC1.PNG of a PSP ISO and creates the icon and background. Returns Nothing when the disc has no icon.
    ''' </summary>
    Public Shared Function CreateImagesFromPspIso(IsoFile As String, Name As String, Optional TargetDirectory As String = Nothing) As GeneratedImages
        Dim Icon As Byte() = ReadFromIso(IsoFile, "\PSP_GAME\ICON0.PNG")
        If Icon Is Nothing Then Return Nothing
        Dim Background As Byte() = ReadFromIso(IsoFile, "\PSP_GAME\PIC1.PNG")
        Return CreateImages(Icon, Name, Background, TargetDirectory)
    End Function

    Private Shared Function ReadFromIso(IsoFile As String, InnerPath As String) As Byte()
        Dim Temp As String = Path.Combine(Path.GetTempPath(), "psfpkg-" + Guid.NewGuid().ToString("N") + ".png")
        Try
            If IsoTools.ExtractFileFromPSPISO(IsoFile, InnerPath, Temp) = "" Then Return Nothing
            Dim Data As Byte() = File.ReadAllBytes(Temp)
            Return If(Data.Length > 0, Data, Nothing)
        Finally
            If File.Exists(Temp) Then File.Delete(Temp)
        End Try
    End Function

    Private Shared Function Decode(Data As Byte()) As SKBitmap
        Dim Bitmap As SKBitmap = If(Data Is Nothing, Nothing, SKBitmap.Decode(Data))
        If Bitmap Is Nothing Then Throw New InvalidDataException("The image could not be read.")
        Return Bitmap
    End Function

    ''' <summary>
    ''' Draws <paramref name="Source"/> on a Width x Height canvas: a blurred, darkened copy fills the canvas and, when
    ''' <paramref name="ForegroundScale"/> &gt; 0, the whole image is drawn on top (aspect ratio kept, centered).
    ''' <paramref name="BackdropBrightness"/> is 1 for no darkening.
    ''' </summary>
    Friend Shared Function Compose(Source As SKBitmap, Width As Integer, Height As Integer, ForegroundScale As Double, BackdropBrightness As Double) As Byte()
        Using Surface As SKSurface = SKSurface.Create(New SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul))
            Dim Canvas As SKCanvas = Surface.Canvas
            Canvas.Clear(SKColors.Black)
            Using SourceImage As SKImage = SKImage.FromBitmap(Source)
                'Backdrop: the image scaled to cover the canvas
                Dim CoverScale As Single = CSng(Math.Max(Width / Source.Width, Height / Source.Height))
                Dim BackdropRect As SKRect = CenteredRect(Source.Width * CoverScale, Source.Height * CoverScale, Width, Height)
                Dim Blur As Single = If(ForegroundScale > 0 OrElse BackdropBrightness < 1, Width / 40.0F, 0)
                Using BackdropPaint As New SKPaint()
                    If Blur > 0 Then BackdropPaint.ImageFilter = SKImageFilter.CreateBlur(Blur, Blur)
                    Dim Brightness As Single = CSng(BackdropBrightness)
                    BackdropPaint.ColorFilter = SKColorFilter.CreateColorMatrix({
                        Brightness, 0, 0, 0, 0,
                        0, Brightness, 0, 0, 0,
                        0, 0, Brightness, 0, 0,
                        0, 0, 0, 1, 0})
                    Canvas.DrawImage(SourceImage, BackdropRect, New SKSamplingOptions(SKCubicResampler.Mitchell), BackdropPaint)
                End Using

                'Foreground: the whole image, aspect ratio kept
                If ForegroundScale > 0 Then
                    Dim FitScale As Single = CSng(Math.Min(Width / Source.Width, Height / Source.Height) * ForegroundScale)
                    Dim ForegroundRect As SKRect = CenteredRect(Source.Width * FitScale, Source.Height * FitScale, Width, Height)
                    Canvas.DrawImage(SourceImage, ForegroundRect, New SKSamplingOptions(SKCubicResampler.Mitchell))
                End If
            End Using
            Using Snapshot As SKImage = Surface.Snapshot()
                Using Encoded As SKData = Snapshot.Encode(SKEncodedImageFormat.Png, 100)
                    Return Encoded.ToArray()
                End Using
            End Using
        End Using
    End Function

    Private Shared Function CenteredRect(RectWidth As Single, RectHeight As Single, Width As Integer, Height As Integer) As SKRect
        Dim Left As Single = (Width - RectWidth) / 2.0F
        Dim Top As Single = (Height - RectHeight) / 2.0F
        Return New SKRect(Left, Top, Left + RectWidth, Top + RectHeight)
    End Function

End Class
