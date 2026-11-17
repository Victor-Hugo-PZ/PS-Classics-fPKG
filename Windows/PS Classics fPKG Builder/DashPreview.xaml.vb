Imports System.Windows.Media.Imaging

''' <summary>
''' Live preview of how the game tile looks on the PS4 / PS5 home screen (an approximation, not a pixel exact copy).
''' Bind <see cref="IconPath"/>, <see cref="BackgroundPath"/> and <see cref="GameTitle"/> to the fields of the form.
''' </summary>
Public Class DashPreview

    Public Shared ReadOnly IconPathProperty As DependencyProperty = Register("IconPath")
    Public Shared ReadOnly BackgroundPathProperty As DependencyProperty = Register("BackgroundPath")
    Public Shared ReadOnly FallbackIconPathProperty As DependencyProperty = Register("FallbackIconPath")
    Public Shared ReadOnly FallbackBackgroundPathProperty As DependencyProperty = Register("FallbackBackgroundPath")
    Public Shared ReadOnly GameTitleProperty As DependencyProperty = Register("GameTitle")

    Public Shared ReadOnly IsPS5Property As DependencyProperty =
        DependencyProperty.Register("IsPS5", GetType(Boolean), GetType(DashPreview), New PropertyMetadata(False, AddressOf OnPreviewPropertyChanged))

    ''' <summary>Read-only text with hints about the selected images (stretching, low resolution).</summary>
    Public Shared ReadOnly HintsProperty As DependencyProperty =
        DependencyProperty.Register("Hints", GetType(String), GetType(DashPreview), New PropertyMetadata(""))

    ''' <summary>The icon currently shown (selected image or emulator default), for thumbnails next to the preview.</summary>
    Public Shared ReadOnly CurrentIconProperty As DependencyProperty =
        DependencyProperty.Register("CurrentIcon", GetType(ImageSource), GetType(DashPreview), New PropertyMetadata(Nothing))

    ''' <summary>The background currently shown (selected image or emulator default).</summary>
    Public Shared ReadOnly CurrentBackgroundProperty As DependencyProperty =
        DependencyProperty.Register("CurrentBackground", GetType(ImageSource), GetType(DashPreview), New PropertyMetadata(Nothing))

    Public Property CurrentIcon As ImageSource
        Get
            Return CType(GetValue(CurrentIconProperty), ImageSource)
        End Get
        Private Set(value As ImageSource)
            SetValue(CurrentIconProperty, value)
        End Set
    End Property

    Public Property CurrentBackground As ImageSource
        Get
            Return CType(GetValue(CurrentBackgroundProperty), ImageSource)
        End Get
        Private Set(value As ImageSource)
            SetValue(CurrentBackgroundProperty, value)
        End Set
    End Property

    Private Shared Function Register(Name As String) As DependencyProperty
        Return DependencyProperty.Register(Name, GetType(String), GetType(DashPreview), New PropertyMetadata("", AddressOf OnPreviewPropertyChanged))
    End Function

    Public Property IconPath As String
        Get
            Return CStr(GetValue(IconPathProperty))
        End Get
        Set(value As String)
            SetValue(IconPathProperty, value)
        End Set
    End Property

    Public Property BackgroundPath As String
        Get
            Return CStr(GetValue(BackgroundPathProperty))
        End Get
        Set(value As String)
            SetValue(BackgroundPathProperty, value)
        End Set
    End Property

    ''' <summary>Image shown when no icon is selected (the default icon of the emulator that ends up in the package).</summary>
    Public Property FallbackIconPath As String
        Get
            Return CStr(GetValue(FallbackIconPathProperty))
        End Get
        Set(value As String)
            SetValue(FallbackIconPathProperty, value)
        End Set
    End Property

    ''' <summary>Image shown when no background is selected.</summary>
    Public Property FallbackBackgroundPath As String
        Get
            Return CStr(GetValue(FallbackBackgroundPathProperty))
        End Get
        Set(value As String)
            SetValue(FallbackBackgroundPathProperty, value)
        End Set
    End Property

    Public Property GameTitle As String
        Get
            Return CStr(GetValue(GameTitleProperty))
        End Get
        Set(value As String)
            SetValue(GameTitleProperty, value)
        End Set
    End Property

    Public Property IsPS5 As Boolean
        Get
            Return CBool(GetValue(IsPS5Property))
        End Get
        Set(value As Boolean)
            SetValue(IsPS5Property, value)
        End Set
    End Property

    Public Property Hints As String
        Get
            Return CStr(GetValue(HintsProperty))
        End Get
        Private Set(value As String)
            SetValue(HintsProperty, value)
        End Set
    End Property

    Private Shared Sub OnPreviewPropertyChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim Preview As DashPreview = TryCast(d, DashPreview)
        If Preview IsNot Nothing AndAlso Preview.IsInitialized Then Preview.UpdatePreview()
    End Sub

    Private Sub DashPreview_Initialized(sender As Object, e As EventArgs) Handles Me.Initialized
        UpdatePreview()
    End Sub

    Private Sub UpdatePreview()
        'Hints come from the shared Core library, so every version of the app shows the same text
        Dim HintLines As List(Of String) = ImageConverter.PreviewHints(IconPath, BackgroundPath)

        'Icon: the selected one, otherwise the default icon of the emulator
        Dim Icon As BitmapSource = ImageLoader.TryLoad(IconPath, 512)
        If Icon Is Nothing AndAlso String.IsNullOrWhiteSpace(IconPath) Then
            Icon = ImageLoader.TryLoad(FallbackIconPath, 512)
        End If

        'Background: the selected one, otherwise the fallback
        Dim Background As BitmapSource = ImageLoader.TryLoad(BackgroundPath, 1280)
        If Background Is Nothing AndAlso String.IsNullOrWhiteSpace(BackgroundPath) Then
            Background = ImageLoader.TryLoad(FallbackBackgroundPath, 1280)
        End If

        CurrentIcon = Icon
        CurrentBackground = Background
        BackgroundImage.Source = Background
        PS4IconImage.Source = Icon
        PS5IconBrush.ImageSource = Icon
        PS4IconPlaceholder.Visibility = If(Icon Is Nothing, Visibility.Visible, Visibility.Collapsed)
        PS5IconPlaceholder.Visibility = PS4IconPlaceholder.Visibility

        Dim TitleText As String = If(String.IsNullOrWhiteSpace(GameTitle), "Game title", GameTitle)
        PS4TitleText.Text = TitleText
        PS5TitleText.Text = TitleText

        PS4Layout.Visibility = If(IsPS5, Visibility.Collapsed, Visibility.Visible)
        PS5Layout.Visibility = If(IsPS5, Visibility.Visible, Visibility.Collapsed)

        Hints = String.Join(vbCrLf, HintLines)
    End Sub

End Class
