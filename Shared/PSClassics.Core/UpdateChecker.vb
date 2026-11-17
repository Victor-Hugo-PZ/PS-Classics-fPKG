Imports System.Net.Http
Imports System.Text.Json

''' <summary>Latest release published on GitHub.</summary>
Public Class UpdateInfo
    Public Property LatestVersion As Version
    Public Property Tag As String = ""
    Public Property Url As String = ""
    Public Property IsNewer As Boolean
End Class

''' <summary>Asks GitHub for the latest release of this fork (one small request, only the version number is read).</summary>
Public Class UpdateChecker

    Public Const Repository As String = "Victor-Hugo-PZ/PS-Classics-fPKG"
    Public Const ReleasesPage As String = "https://github.com/" + Repository + "/releases/latest"
    Private Const LatestReleaseApi As String = "https://api.github.com/repos/" + Repository + "/releases/latest"

    ''' <summary>Version of this build of the app (set by the release scripts).</summary>
    Public Shared Function CurrentVersion() As Version
        Dim Found As Version = GetType(UpdateChecker).Assembly.GetName().Version
        Return If(Found, New Version(0, 0))
    End Function

    ''' <summary>"v2.2.0", "2.2", "v2.2.0-beta" -> Version (Nothing when the tag is not a version).</summary>
    Public Shared Function ParseVersion(Tag As String) As Version
        If String.IsNullOrWhiteSpace(Tag) Then Return Nothing
        Dim Text As String = Tag.Trim().TrimStart("v"c, "V"c)
        Dim Dash As Integer = Text.IndexOf("-"c)
        If Dash >= 0 Then Text = Text.Substring(0, Dash)
        Dim Parsed As Version = Nothing
        If Not Version.TryParse(If(Text.Contains("."c), Text, Text + ".0"), Parsed) Then Return Nothing
        Return Parsed
    End Function

    ''' <summary>Reads the JSON of GitHub's "latest release" answer.</summary>
    Public Shared Function ParseLatestRelease(Json As String, Current As Version) As UpdateInfo
        Using Document As JsonDocument = JsonDocument.Parse(Json)
            Dim Root As JsonElement = Document.RootElement
            Dim Tag As String = Root.GetProperty("tag_name").GetString()
            Dim Url As String = ReleasesPage
            Dim UrlElement As JsonElement
            If Root.TryGetProperty("html_url", UrlElement) Then Url = UrlElement.GetString()
            Dim Latest As Version = ParseVersion(Tag)
            Return New UpdateInfo With {
                .Tag = Tag,
                .Url = Url,
                .LatestVersion = Latest,
                .IsNewer = Latest IsNot Nothing AndAlso IsNewer(Latest, Current)
            }
        End Using
    End Function

    ''' <summary>Checks GitHub. Returns Nothing when it could not be checked (offline, rate limit...): never throws.</summary>
    Public Shared Async Function CheckAsync(Optional Current As Version = Nothing) As Task(Of UpdateInfo)
        Try
            Using Client As HttpClient = CoverArt.CreateClient()
                Client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json")
                Dim Json As String = Await Client.GetStringAsync(LatestReleaseApi).ConfigureAwait(False)
                Return ParseLatestRelease(Json, If(Current, CurrentVersion()))
            End Using
        Catch ex As Exception
            Return Nothing
        End Try
    End Function

    ''' <summary>True when the automatic check should run now (enabled and last check more than a day ago).</summary>
    Public Shared Function IsCheckDue(Settings As AppSettings, Now As DateTime) As Boolean
        Return Settings.CheckForUpdates AndAlso (Now - Settings.LastUpdateCheck).TotalHours >= 24
    End Function

    ''' <summary>True when <paramref name="Latest"/> is a newer release than <paramref name="Current"/> (major.minor.build).</summary>
    Public Shared Function IsNewer(Latest As Version, Current As Version) As Boolean
        Return Normalize(Latest) > Normalize(Current)
    End Function

    ''' <summary>Compares only major.minor.build (a missing part counts as 0).</summary>
    Private Shared Function Normalize(Value As Version) As Version
        Return New Version(Math.Max(0, Value.Major), Math.Max(0, Value.Minor), Math.Max(0, Value.Build))
    End Function

End Class
