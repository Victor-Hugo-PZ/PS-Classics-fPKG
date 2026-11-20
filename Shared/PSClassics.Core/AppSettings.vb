Imports System.IO
Imports System.Text.Json

''' <summary>User preferences, saved as JSON in the per-user data folder (shared by every version of the app on this computer).</summary>
Public Class AppSettings

    ''' <summary>Language code ("en", "pt-BR"); empty = language of the system.</summary>
    Public Property Language As String = ""
    ''' <summary>Shows one step of the form at a time (Game, Images, Options, Review).</summary>
    Public Property GuidedMode As Boolean = True
    Public Property CheckForUpdates As Boolean = True
    ''' <summary>Runs the quick package check right after a build.</summary>
    Public Property CheckPackageAfterBuild As Boolean = True
    Public Property LastUpdateCheck As DateTime = DateTime.MinValue
    ''' <summary>A newer version the user chose to skip (no reminder until an even newer one).</summary>
    Public Property SkippedVersion As String = ""
    ''' <summary>Latest release found by the last update check (shown until the user updates or skips it).</summary>
    Public Property LatestVersionTag As String = ""
    Public Property LatestVersionUrl As String = ""
    ''' <summary>Output folder of the queue window, kept for the next time.</summary>
    Public Property QueueOutputFolder As String = ""
    ''' <summary>Output folder of the last package built with the form, offered first the next time.</summary>
    Public Property LastOutputFolder As String = ""

    Private Shared _Current As AppSettings

    Public Shared ReadOnly Property DefaultFile As String
        Get
            Return Path.Combine(AppPaths.DataDirectory, "settings.json")
        End Get
    End Property

    ''' <summary>The settings of this computer, loaded on first use.</summary>
    Public Shared Property Current As AppSettings
        Get
            If _Current Is Nothing Then _Current = Load(DefaultFile)
            Return _Current
        End Get
        Set(value As AppSettings)
            _Current = value
        End Set
    End Property

    ''' <summary>The language to use: the saved choice, or the system language.</summary>
    Public Function EffectiveLanguage() As String
        Return If(String.IsNullOrWhiteSpace(Language), Localizer.SystemLanguage(), Localizer.Normalize(Language))
    End Function

    ''' <summary>Loads settings from a file. A missing or damaged file gives the defaults.</summary>
    Public Shared Function Load(SettingsFile As String) As AppSettings
        Try
            If File.Exists(SettingsFile) Then
                Dim Loaded As AppSettings = JsonSerializer.Deserialize(Of AppSettings)(File.ReadAllText(SettingsFile))
                If Loaded IsNot Nothing Then Return Loaded
            End If
        Catch ex As Exception
            'Damaged file: start again with the defaults
        End Try
        Return New AppSettings()
    End Function

    Public Sub Save(Optional SettingsFile As String = Nothing)
        Dim Target As String = If(SettingsFile, DefaultFile)
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(Target))
            File.WriteAllText(Target, JsonSerializer.Serialize(Me, New JsonSerializerOptions With {.WriteIndented = True}))
        Catch ex As Exception
            'Preferences are not essential: never stop the app because they could not be written
        End Try
    End Sub

End Class
