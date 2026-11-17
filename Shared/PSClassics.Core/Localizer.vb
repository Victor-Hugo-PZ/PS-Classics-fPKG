Imports System.Globalization
Imports System.IO
Imports System.Runtime.CompilerServices

''' <summary>
''' Translations of the user interface. The English text itself is the key: <c>Localizer.T("Build fPKG")</c> returns the
''' text in the current language, or the English text when there is no translation. Translations live in
''' Localization/&lt;code&gt;.tsv (one "English TAB translation" pair per line; a line break inside a text is written \n).
''' Both apps (WPF and Avalonia) use it, so they show the same texts.
''' </summary>
Public NotInheritable Class Localizer

    Public Const English As String = "en"
    Public Const Portuguese As String = "pt-BR"

    ''' <summary>Languages offered to the user (code, name in that language).</summary>
    Public Shared ReadOnly Property Languages As IReadOnlyList(Of KeyValuePair(Of String, String)) = New List(Of KeyValuePair(Of String, String)) From {
        New KeyValuePair(Of String, String)(English, "English"),
        New KeyValuePair(Of String, String)(Portuguese, "Português (Brasil)")
    }

    Private Shared ReadOnly Tables As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)
    Private Shared _Language As String = English

    ''' <summary>Raised after <see cref="Language"/> changed. The apps re-translate their windows.</summary>
    Public Shared Event LanguageChanged As EventHandler

    ''' <summary>Code of the current language ("en" or "pt-BR").</summary>
    Public Shared Property Language As String
        Get
            Return _Language
        End Get
        Set(value As String)
            Dim Code As String = Normalize(value)
            If Code = _Language Then Return
            _Language = Code
            RaiseEvent LanguageChanged(Nothing, EventArgs.Empty)
        End Set
    End Property

    ''' <summary>Language of the operating system when it is supported, else English.</summary>
    Public Shared Function SystemLanguage() As String
        Return Normalize(CultureInfo.CurrentUICulture.Name)
    End Function

    ''' <summary>Maps any culture name to a supported language code ("pt", "pt-PT", "pt-BR" -> "pt-BR"; others -> "en").</summary>
    Public Shared Function Normalize(Code As String) As String
        If String.IsNullOrWhiteSpace(Code) Then Return English
        If Code.StartsWith("pt", StringComparison.OrdinalIgnoreCase) Then Return Portuguese
        Return English
    End Function

    ''' <summary>
    ''' Translates an English text. Line breaks are kept; a literal \n (used in VB code, which has no escape sequences)
    ''' becomes a line break.
    ''' </summary>
    Public Shared Function T(EnglishText As String) As String
        If String.IsNullOrEmpty(EnglishText) Then Return EnglishText
        Dim Translated As String = Nothing
        If _Language <> English AndAlso Table(_Language).TryGetValue(KeyOf(EnglishText), Translated) Then Return Translated.Replace("\n", vbLf)
        Return EnglishText.Replace("\n", vbLf)
    End Function

    ''' <summary>Translates an English format string ("{0} problem(s) found") and fills it in.</summary>
    Public Shared Function F(EnglishFormat As String, ParamArray Args As Object()) As String
        Return String.Format(CultureInfo.CurrentCulture, T(EnglishFormat), Args)
    End Function

    ''' <summary>True when <paramref name="EnglishText"/> has a translation in <paramref name="LanguageCode"/>.</summary>
    Public Shared Function HasTranslation(EnglishText As String, LanguageCode As String) As Boolean
        Return Table(Normalize(LanguageCode)).ContainsKey(KeyOf(EnglishText))
    End Function

    ''' <summary>All the translations of a language (key: English text with line breaks written as \n).</summary>
    Public Shared Function TranslationsOf(LanguageCode As String) As IReadOnlyDictionary(Of String, String)
        Return Table(Normalize(LanguageCode))
    End Function

    ''' <summary>The key of a text: line breaks (CR LF or LF) written as \n.</summary>
    Public Shared Function KeyOf(Text As String) As String
        Return Text.Replace(vbCrLf, "\n").Replace(vbLf, "\n")
    End Function

#Region "Translation of controls"

    Private Class Tracked
        Public Source As String
        Public Shown As String
    End Class

    Private Shared ReadOnly TrackedTexts As New ConditionalWeakTable(Of Object, Dictionary(Of String, Tracked))

    ''' <summary>
    ''' Translates a text shown by a control property and remembers its English source, so the property can be translated
    ''' again (or back to English) when the language changes. When the app changed the text in the meantime, the new
    ''' text becomes the source. Returns the text to show.
    ''' </summary>
    Public Shared Function TranslateProperty(Owner As Object, PropertyName As String, CurrentText As String) As String
        If Owner Is Nothing OrElse CurrentText Is Nothing Then Return CurrentText
        Dim Texts As Dictionary(Of String, Tracked) = TrackedTexts.GetOrCreateValue(Owner)
        Dim Entry As Tracked = Nothing
        If Not Texts.TryGetValue(PropertyName, Entry) Then
            Entry = New Tracked()
            Texts(PropertyName) = Entry
        End If
        If Entry.Shown Is Nothing OrElse CurrentText <> Entry.Shown Then Entry.Source = CurrentText
        Entry.Shown = T(Entry.Source)
        Return Entry.Shown
    End Function

#End Region

    Private Shared Function Table(Code As String) As Dictionary(Of String, String)
        SyncLock Tables
            Dim Found As Dictionary(Of String, String) = Nothing
            If Tables.TryGetValue(Code, Found) Then Return Found
            Found = LoadTable(Code)
            Tables(Code) = Found
            Return Found
        End SyncLock
    End Function

    Private Shared Function LoadTable(Code As String) As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)(StringComparer.Ordinal)
        If Code = English Then Return Result
        Using Stream As Stream = GetType(Localizer).Assembly.GetManifestResourceStream("PSClassics.Localization." + Code + ".tsv")
            If Stream Is Nothing Then Return Result
            Using Reader As New StreamReader(Stream, Text.Encoding.UTF8)
                For Each Pair In ParseTable(Reader.ReadToEnd())
                    Result(Pair.Key) = Pair.Value
                Next
            End Using
        End Using
        Return Result
    End Function

    ''' <summary>Parses the lines of a translation file. Empty lines and lines starting with # are ignored.</summary>
    Public Shared Function ParseTable(Content As String) As List(Of KeyValuePair(Of String, String))
        Dim Pairs As New List(Of KeyValuePair(Of String, String))
        For Each RawLine As String In Content.Split({vbCrLf, vbLf}, StringSplitOptions.None)
            If RawLine.Trim() = "" OrElse RawLine.StartsWith("#") Then Continue For
            Dim Tab As Integer = RawLine.IndexOf(vbTab)
            If Tab <= 0 Then Continue For
            Pairs.Add(New KeyValuePair(Of String, String)(RawLine.Substring(0, Tab), RawLine.Substring(Tab + 1)))
        Next
        Return Pairs
    End Function

End Class
