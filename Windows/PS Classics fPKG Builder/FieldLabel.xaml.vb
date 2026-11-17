Imports System.Windows.Media

''' <summary>
''' Label of a form row with a help button and a "Required" / "Optional" badge.
''' Set <see cref="LabelText"/>, <see cref="HelpText"/> and <see cref="IsRequired"/> in XAML.
''' </summary>
Public Class FieldLabel

    Private _LabelText As String = ""
    Private _HelpText As String = ""

    Public Property LabelText As String
        Get
            Return _LabelText
        End Get
        Set(value As String)
            _LabelText = value
            LabelTextBlock.Text = value
        End Set
    End Property

    ''' <summary>Explanation shown in the tooltip and in the dialog opened by the help button.</summary>
    Public Property HelpText As String
        Get
            Return _HelpText
        End Get
        Set(value As String)
            _HelpText = value
            HelpButton.ToolTip = value
        End Set
    End Property

    Public Property IsRequired As Boolean
        Set(value As Boolean)
            BadgeText.Text = If(value, "Required", "Optional")
            BadgeText.Foreground = If(value,
                                      TryFindBrush("TextOnAccentFillColorPrimaryBrush", Brushes.White),
                                      TryFindBrush("TextFillColorSecondaryBrush", Brushes.LightGray))
            BadgeBorder.Background = If(value,
                                        TryFindBrush("AccentFillColorDefaultBrush", Brushes.SteelBlue),
                                        TryFindBrush("ControlFillColorSecondaryBrush", Brushes.DimGray))
        End Set
        Get
            Return BadgeText.Text = "Required"
        End Get
    End Property

    Private Function TryFindBrush(Key As String, Fallback As Brush) As Brush
        Dim Found As Object = TryFindResource(Key)
        Return If(TypeOf Found Is Brush, CType(Found, Brush), Fallback)
    End Function

    Private Sub HelpButton_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles HelpButton.Click
        MsgBox(_HelpText, MsgBoxStyle.Information, _LabelText)
    End Sub

End Class
