Imports System.Text.RegularExpressions

''' <summary>A fix that the app can start for the user (shown as a button).</summary>
Public Enum FixAction
    None
    ''' <summary>Pick the game file again.</summary>
    SelectGame
    ''' <summary>Open the Wine setup (Linux and macOS).</summary>
    WineSetup
    ''' <summary>Show the Tools folder.</summary>
    OpenToolsFolder
    ''' <summary>Clear the icon and the background fields.</summary>
    ClearImages
    ''' <summary>Build again (the output folder is asked again).</summary>
    TryAgain
End Enum

''' <summary>The button text of each fix.</summary>
Public Class FixActions

    Public Shared Function Label(Action As FixAction) As String
        Select Case Action
            Case FixAction.SelectGame
                Return Localizer.T("Select the game file again")
            Case FixAction.WineSetup
                Return Localizer.T("Open the Wine setup")
            Case FixAction.OpenToolsFolder
                Return Localizer.T("Show the Tools folder")
            Case FixAction.ClearImages
                Return Localizer.T("Clear the icon and the background")
            Case FixAction.TryAgain
                Return Localizer.T("Choose another output folder and build again")
            Case Else
                Return ""
        End Select
    End Function

    ''' <summary>Every English button text (for the translation tests).</summary>
    Public Shared Function AllTexts() As IEnumerable(Of String)
        Return {"Select the game file again", "Open the Wine setup", "Show the Tools folder", "Clear the icon and the background", "Choose another output folder and build again"}
    End Function

End Class

''' <summary>A build failure explained for a beginner (texts already translated).</summary>
Public Class FriendlyError
    Public Property Title As String = ""
    Public Property Explanation As String = ""
    Public Property Suggestion As String = ""
    ''' <summary>The most relevant line of the technical output (empty when none was found).</summary>
    Public Property KeyLine As String = ""
    ''' <summary>What the app can do to fix it (a button), and the text of that button (already translated).</summary>
    Public Property Action As FixAction = FixAction.None
    Public Property ActionLabel As String = ""
End Class

''' <summary>
''' Turns the technical output of a failed build (publishing tool, Wine, exceptions) into a plain explanation with a fix.
''' Unknown errors get a generic explanation; the technical details are always shown too.
''' </summary>
Public Class ErrorExplainer

    Private Class Rule
        Public Pattern As Regex
        Public Explanation As String
        Public Suggestion As String
        Public Action As FixAction
    End Class

    Private Shared Function R(Pattern As String, Explanation As String, Suggestion As String, Optional Action As FixAction = FixAction.None) As Rule
        Return New Rule With {.Pattern = New Regex(Pattern, RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant), .Explanation = Explanation, .Suggestion = Suggestion, .Action = Action}
    End Function

    'Order matters: the first matching rule wins
    Private Shared ReadOnly Rules As Rule() = {
        R("Wine was not found|wine: not found|WineNotFound",
          "Wine is not installed or was not found.",
          "Install Wine and winetricks, then press 'Wine setup' at the bottom left.", FixAction.WineSetup),
        R("msvcr90|vcrun2008|side-by-side|0xc0000135|0xc000007b",
          "The Visual C++ 2008 runtime is missing in Wine.",
          "Press 'Wine setup' at the bottom left and then 'Set up Wine'.", FixAction.WineSetup),
        R("0x0eedfade|nodrv_CreateWindow|no driver could be loaded",
          "Wine could not open a window (no display was found).",
          "Start the app from your desktop session, not from a remote terminal."),
        R("not enough space|disk full|no space left|There is not enough space",
          "The disk is full.",
          "Free some space or choose an output folder on another drive.", FixAction.TryAgain),
        R("being used by another process|sharing violation",
          "A file is open in another program.",
          "Close programs that may use the game or the package (emulators, players, antivirus scans) and try again."),
        R("Access to the path .* is denied|Access is denied|UnauthorizedAccess|Permission denied|read-only",
          "The app is not allowed to write to a folder.",
          "Choose another output folder (for example in Documents) or move the app to a folder you own.", FixAction.TryAgain),
        R("path is too long|PathTooLong|too long",
          "A file path is too long for the publishing tool.",
          "Move the game to a short folder (for example C:\Games) and choose a short output folder.", FixAction.TryAgain),
        R("\.CUE file|cue sheet",
          "The .CUE file of the game is missing.",
          "Put the .cue file next to the .bin file, with the same name.", FixAction.SelectGame),
        R("GP4 project was not generated",
          "The project generator (gengp4) failed.",
          "Check that the Tools folder is complete. On Linux and macOS, check the Wine setup.", FixAction.OpenToolsFolder),
        R("image could not be read|image could not be resized",
          "The icon or the background could not be read.",
          "Use a PNG, JPG, BMP, GIF or WebP image, or leave the field empty.", FixAction.ClearImages),
        R("game ID could not be determined",
          "The PS2 game ID could not be found.",
          "Type the NP title of the game (9 characters, for example SLUS21258)."),
        R("Could not find file|FileNotFound|was not found|does not exist|No such file",
          "A file needed by the build was not found.",
          "A game file or the Tools folder may have been moved. Select the files again and keep the Tools folder next to the program.", FixAction.SelectGame),
        R("content.?id|invalid character|illegal character",
          "The publishing tool rejected the title or the NP title.",
          "Use only letters and digits in the NP title and avoid special symbols in the title.")
    }

    Public Shared Function Explain(Details As String) As FriendlyError
        Dim Text As String = If(Details, "")
        Dim Result As New FriendlyError With {.Title = Localizer.T("The fPKG could not be created"), .KeyLine = FindKeyLine(Text)}
        For Each Item As Rule In Rules
            If Item.Pattern.IsMatch(Text) Then
                Result.Explanation = Localizer.T(Item.Explanation)
                Result.Suggestion = Localizer.T(Item.Suggestion)
                Result.Action = Item.Action
                Result.ActionLabel = FixActions.Label(Item.Action)
                Return Result
            End If
        Next
        Result.Explanation = Localizer.T("The publishing tool stopped with an error.")
        Result.Suggestion = Localizer.T("Check the technical details below. Try again; if it keeps failing, report it with the details (Copy button).")
        Return Result
    End Function

    ''' <summary>The first line that looks like an error message.</summary>
    Public Shared Function FindKeyLine(Details As String) As String
        For Each Line As String In If(Details, "").Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim Trimmed As String = Line.Trim()
            If Regex.IsMatch(Trimmed, "\[Error\]|error|exception|failed|denied|not found", RegexOptions.IgnoreCase) Then Return Trimmed
        Next
        Return ""
    End Function

    ''' <summary>Every English text this class can show (for the translation tests).</summary>
    Public Shared Function AllTexts() As IEnumerable(Of String)
        Return Rules.SelectMany(Function(Item) {Item.Explanation, Item.Suggestion}).Concat({
            "The fPKG could not be created", "The publishing tool stopped with an error.",
            "Check the technical details below. Try again; if it keeps failing, report it with the details (Copy button)."})
    End Function

    ''' <summary>The full message of a friendly error, for a dialog.</summary>
    Public Shared Function Describe([Error] As FriendlyError) As String
        Dim Parts As New List(Of String) From {[Error].Explanation}
        If [Error].Suggestion <> "" Then Parts.Add("→ " + [Error].Suggestion)
        Return String.Join(vbLf + vbLf, Parts)
    End Function

End Class
