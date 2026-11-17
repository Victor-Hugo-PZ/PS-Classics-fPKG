''' <summary>
''' Translates the texts of a window (labels, buttons, hints, tooltips, placeholders) with <see cref="Localizer"/>.
''' The XAML stays in English; calling it again after the language changed switches the texts (back) to the new language.
''' Combo box items keep their values (they are option values) and the home screen preview imitates the console.
''' </summary>
Public Module UiTranslator

    Public Sub Translate(Root As DependencyObject)
        If Root Is Nothing Then Return
        If TypeOf Root Is DashPreview OrElse TypeOf Root Is ComboBoxItem Then Return

        If TypeOf Root Is FieldLabel Then
            Dim Label As FieldLabel = CType(Root, FieldLabel)
            Label.LabelText = Localizer.TranslateProperty(Label, "LabelText", Label.LabelText)
            Label.HelpText = Localizer.TranslateProperty(Label, "HelpText", Label.HelpText)
            Label.IsRequired = Label.IsRequired
            Return
        End If

        If TypeOf Root Is Window Then
            Dim Window As Window = CType(Root, Window)
            Window.Title = Localizer.TranslateProperty(Window, "Title", Window.Title)
        End If

        If TypeOf Root Is TextBlock Then
            Dim Text As TextBlock = CType(Root, TextBlock)
            If Not BindingOperations.IsDataBound(Text, TextBlock.TextProperty) Then
                Text.Text = Localizer.TranslateProperty(Text, "Text", Text.Text)
            End If
        ElseIf TypeOf Root Is Wpf.Ui.Controls.TextBox Then
            Dim Box As Wpf.Ui.Controls.TextBox = CType(Root, Wpf.Ui.Controls.TextBox)
            Box.PlaceholderText = Localizer.TranslateProperty(Box, "PlaceholderText", Box.PlaceholderText)
        End If

        If TypeOf Root Is HeaderedContentControl Then
            Dim Headered As HeaderedContentControl = CType(Root, HeaderedContentControl)
            If TypeOf Headered.Header Is String Then Headered.Header = Localizer.TranslateProperty(Headered, "Header", CStr(Headered.Header))
        End If
        If TypeOf Root Is ContentControl AndAlso Not TypeOf Root Is Window Then
            Dim Control As ContentControl = CType(Root, ContentControl)
            If TypeOf Control.Content Is String Then Control.Content = Localizer.TranslateProperty(Control, "Content", CStr(Control.Content))
        End If
        If TypeOf Root Is Wpf.Ui.Controls.TitleBar Then
            Dim Bar As Wpf.Ui.Controls.TitleBar = CType(Root, Wpf.Ui.Controls.TitleBar)
            If TypeOf Bar.Title Is String AndAlso CStr(Bar.Title) <> "PS Classics fPKG Builder" Then Bar.Title = Localizer.TranslateProperty(Bar, "Title", CStr(Bar.Title))
        End If
        If TypeOf Root Is FrameworkElement Then
            Dim Element As FrameworkElement = CType(Root, FrameworkElement)
            If TypeOf Element.ToolTip Is String Then Element.ToolTip = Localizer.TranslateProperty(Element, "ToolTip", CStr(Element.ToolTip))
        End If

        For Each Child As Object In LogicalTreeHelper.GetChildren(Root)
            If TypeOf Child Is DependencyObject Then Translate(CType(Child, DependencyObject))
        Next
    End Sub

    ''' <summary>Translates a window now and every time the language changes, until it is closed.</summary>
    Public Sub Attach(Window As Window)
        Translate(Window)
        Dim Handler As EventHandler = Sub(sender, e) Window.Dispatcher.Invoke(Sub() Translate(Window))
        AddHandler Localizer.LanguageChanged, Handler
        AddHandler Window.Closed, Sub(sender, e) RemoveHandler Localizer.LanguageChanged, Handler
    End Sub

    ''' <summary>Message box with translated text and title.</summary>
    Public Function Message(Text As String, Optional Style As MsgBoxStyle = MsgBoxStyle.OkOnly, Optional Title As String = Nothing) As MsgBoxResult
        Return MsgBox(Text, Style, If(Title, "PS Classics fPKG Builder"))
    End Function

End Module
