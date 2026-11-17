Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

''' <summary>How a package worked on the console, as noted by the user.</summary>
Public Enum ConsoleResult
    NotTested
    Works
    WorksWithProblems
    Crashes
    DoesNotStart
End Enum

''' <summary>One package build, with the options used and what happened on the console.</summary>
Public Class HistoryEntry
    Public Property Id As String = Guid.NewGuid().ToString("N")
    Public Property CreatedAt As DateTime = DateTime.Now
    ''' <summary>"PS1", "PS2" or "PSP".</summary>
    Public Property Platform As String = ""
    Public Property Title As String = ""
    Public Property NPTitle As String = ""
    Public Property PackagePath As String = ""
    Public Property OutputFolder As String = ""
    ''' <summary>Emulator and options, one per line (English, technical values).</summary>
    Public Property Settings As String = ""
    Public Property Succeeded As Boolean
    <JsonConverter(GetType(JsonStringEnumConverter))>
    Public Property ConsoleResult As ConsoleResult = ConsoleResult.NotTested
    Public Property Notes As String = ""
End Class

''' <summary>
''' List of the packages built on this computer (newest first), saved as JSON in the per-user data folder.
''' Lets the user compare variants of a game ("Rogue v1 crashed, Jak v2 works").
''' </summary>
Public Class BuildHistory

    Public Const MaximumEntries As Integer = 500

    Public Shared ReadOnly Property DefaultFile As String
        Get
            Return Path.Combine(AppPaths.DataDirectory, "history.json")
        End Get
    End Property

    ''' <summary>English names of the console results, in the order of the enum (keys of <see cref="Localizer"/>).</summary>
    Public Shared ReadOnly Property ConsoleResultNames As IReadOnlyList(Of String) = New List(Of String) From {
        "Not tested yet", "Works", "Works with problems", "Crashes", "Does not start"
    }

    Public Shared Function Load(Optional HistoryFile As String = Nothing) As List(Of HistoryEntry)
        Dim Source As String = If(HistoryFile, DefaultFile)
        Try
            If File.Exists(Source) Then
                Dim Entries As List(Of HistoryEntry) = JsonSerializer.Deserialize(Of List(Of HistoryEntry))(File.ReadAllText(Source))
                If Entries IsNot Nothing Then Return Entries.OrderByDescending(Function(Entry) Entry.CreatedAt).ToList()
            End If
        Catch ex As Exception
            'Damaged file: start a new history
        End Try
        Return New List(Of HistoryEntry)
    End Function

    Public Shared Sub Save(Entries As IEnumerable(Of HistoryEntry), Optional HistoryFile As String = Nothing)
        Dim Target As String = If(HistoryFile, DefaultFile)
        Try
            Directory.CreateDirectory(Path.GetDirectoryName(Target))
            Dim Kept As List(Of HistoryEntry) = Entries.OrderByDescending(Function(Entry) Entry.CreatedAt).Take(MaximumEntries).ToList()
            File.WriteAllText(Target, JsonSerializer.Serialize(Kept, New JsonSerializerOptions With {.WriteIndented = True}))
        Catch ex As Exception
            'The history is not essential: never stop the app because it could not be written
        End Try
    End Sub

    ''' <summary>Adds an entry (or replaces the entry with the same Id) and saves the history.</summary>
    Public Shared Sub AddOrUpdate(Entry As HistoryEntry, Optional HistoryFile As String = Nothing)
        Dim Entries As List(Of HistoryEntry) = Load(HistoryFile)
        Entries.RemoveAll(Function(Existing) Existing.Id = Entry.Id)
        Entries.Insert(0, Entry)
        Save(Entries, HistoryFile)
    End Sub

    Public Shared Sub Remove(Id As String, Optional HistoryFile As String = Nothing)
        Dim Entries As List(Of HistoryEntry) = Load(HistoryFile)
        Entries.RemoveAll(Function(Existing) Existing.Id = Id)
        Save(Entries, HistoryFile)
    End Sub

#Region "Settings summaries"

    Public Shared Function Summarize(Request As PS1BuildRequest) As String
        Dim Lines As New List(Of String) From {"Emulator: ps1hd", "Upscaling: " + Request.Upscaling}
        Dim Discs As Integer = {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4}.Count(Function(Disc) Not String.IsNullOrWhiteSpace(Disc))
        If Discs > 1 Then Lines.Add("Discs: " + Discs.ToString())
        AddFlag(Lines, Request.SkipBootlogo, "Skip boot logo")
        AddFlag(Lines, Request.EmulateAnalogSticks, "Emulate analog sticks")
        AddFlag(Lines, Request.Force60Hz, "Force 60 Hz")
        AddFlag(Lines, Request.Guncon, "GunCon")
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.TxtConfigFile), "TXT config: " + Path.GetFileName(Request.TxtConfigFile))
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.LuaConfigFile), "LUA config: " + Path.GetFileName(Request.LuaConfigFile))
        Return String.Join(vbLf, Lines)
    End Function

    Public Shared Function Summarize(Request As PS2BuildRequest) As String
        Dim Lines As New List(Of String) From {
            "Emulator: " + Request.Emulator,
            "Upscaling: " + Request.Upscaling,
            "Uprender: " + Request.Uprender,
            "Display mode: " + Request.DisplayMode
        }
        Dim Discs As Integer = {Request.Disc1, Request.Disc2, Request.Disc3, Request.Disc4, Request.Disc5}.Count(Function(Disc) Not String.IsNullOrWhiteSpace(Disc))
        If Discs > 1 Then Lines.Add("Discs: " + Discs.ToString())
        If Request.MultitapIndex > 0 Then Lines.Add("Multitap: " + Request.MultitapIndex.ToString())
        AddFlag(Lines, Request.FixGraphics, "Fix graphics")
        AddFlag(Lines, Request.ImproveSpeed, "Improve speed")
        AddFlag(Lines, Request.DisableMTVU, "Disable MTVU")
        AddFlag(Lines, Request.DisableInstantVIF1Transfer, "Disable instant VIF1 transfer")
        AddFlag(Lines, Request.UseWidescreenPatch, "Widescreen patch")
        AddFlag(Lines, Not Request.UseCompression, "No compression")
        AddFlag(Lines, Request.AddTxtConfigFromDatabase, "TXT config from database")
        AddFlag(Lines, Request.AddLuaConfigFromDatabase, "LUA config from database")
        AddFlag(Lines, Request.AddPS3ConfigFromDatabase, "PS3 config from database")
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.TxtConfigFile), "TXT config: " + Path.GetFileName(Request.TxtConfigFile))
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.LuaConfigFile), "LUA config: " + Path.GetFileName(Request.LuaConfigFile))
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.MemoryCardFile), "Memory card: " + Path.GetFileName(Request.MemoryCardFile))
        Return String.Join(vbLf, Lines)
    End Function

    Public Shared Function Summarize(Request As PSPBuildRequest) As String
        Dim Lines As New List(Of String) From {"Emulator: psphd"}
        AddFlag(Lines, Not String.IsNullOrWhiteSpace(Request.ConfigFile), "Custom config: " + Path.GetFileName(Request.ConfigFile))
        Return String.Join(vbLf, Lines)
    End Function

    Private Shared Sub AddFlag(Lines As List(Of String), Condition As Boolean, Text As String)
        If Condition Then Lines.Add(Text)
    End Sub

#End Region

End Class
