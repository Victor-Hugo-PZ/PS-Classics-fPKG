Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text.RegularExpressions

''' <summary>Where a cover was found. Only <see cref="IdCollection"/> matches the game ID directly.</summary>
Public Enum CoverSource
    IdCollection
    NameDatabase
    TitleSearch
End Enum

''' <summary>A cover image and where it was found.</summary>
Public Class CoverFound
    Public Property Image As Byte()
    Public Property Source As CoverSource
End Class

''' <summary>
''' Finds the cover of a PS1 or PS2 game in three steps and stops at the first one that works:
'''  1. the ID collection (xlenore covers, matched by the game ID) - the most exact;
'''  2. the box art of libretro, found by the game name that the Redump database (libretro-database) gives to the game ID;
'''  3. the box art of libretro, found by the typed title - the last resort, less exact.
''' The Redump names are cached for 30 days, so the database is downloaded once a month.
''' </summary>
Public Class CoverSources

    Private Const DatCacheDays As Integer = 30
    Private Const BoxArtBase As String = "https://raw.githubusercontent.com/libretro-thumbnails/"
    Private Const DatBase As String = "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/"

    ''' <summary>Name of the libretro box art repository and of the Redump database file of a platform.</summary>
    Private Shared Function LibretroOf(PlatformName As String) As (Repo As String, DatFileName As String)
        Select Case PlatformName
            Case "PS1"
                Return ("Sony_-_PlayStation", "Sony - PlayStation.dat")
            Case "PS2"
                Return ("Sony_-_PlayStation_2", "Sony - PlayStation 2.dat")
            Case Else
                Return ("", "")
        End Select
    End Function

    ''' <summary>Finds a cover. Returns Nothing when no step has it. Throws only when every step failed with an error.</summary>
    Public Shared Async Function FindCoverAsync(PlatformName As String, GameIdOrNPTitle As String, Title As String,
                                                Optional CacheDirectory As String = Nothing, Optional Client As HttpClient = Nothing) As Task(Of CoverFound)
        Dim OwnClient As Boolean = Client Is Nothing
        If OwnClient Then Client = CoverArt.CreateClient()
        Dim LastError As Exception = Nothing
        Try
            Dim Serial As String = CoverArt.NormalizeSerial(GameIdOrNPTitle)

            ' 1. The ID collection
            If Serial <> "" Then
                Try
                    Dim Cover As Byte() = Await CoverArt.DownloadCoverAsync(PlatformName, Serial, Client)
                    If Cover IsNot Nothing Then Return New CoverFound With {.Image = Cover, .Source = CoverSource.IdCollection}
                Catch ex As Exception
                    LastError = ex
                End Try
            End If

            Dim Libretro = LibretroOf(PlatformName)
            If Libretro.Repo = "" Then
                If LastError IsNot Nothing Then Throw LastError
                Return Nothing
            End If

            ' 2. The box art of the name that the Redump database gives to the game ID
            If Serial <> "" Then
                Try
                    Dim Names As Dictionary(Of String, String) = Await LoadSerialNamesAsync(PlatformName, Libretro.DatFileName, Client, CacheDirectory)
                    Dim RedumpName As String = Nothing
                    If Names.TryGetValue(Serial, RedumpName) Then
                        For Each Candidate As String In LibretroNameCandidates(RedumpName)
                            Dim Image As Byte() = Await DownloadBoxArtAsync(Libretro.Repo, Candidate, Client)
                            If Image IsNot Nothing Then Return New CoverFound With {.Image = Image, .Source = CoverSource.NameDatabase}
                        Next
                    End If
                Catch ex As Exception
                    LastError = ex
                End Try
            End If

            ' 3. The typed title (less exact: the names of the two databases can differ)
            For Each Candidate As String In TitleNameCandidates(Title)
                Try
                    Dim Image As Byte() = Await DownloadBoxArtAsync(Libretro.Repo, Candidate, Client)
                    If Image IsNot Nothing Then Return New CoverFound With {.Image = Image, .Source = CoverSource.TitleSearch}
                Catch ex As Exception
                    LastError = ex
                End Try
            Next

            If LastError IsNot Nothing Then Throw LastError
            Return Nothing
        Finally
            If OwnClient Then Client.Dispose()
        End Try
    End Function

    ''' <summary>Box art of a game name in libretro's Named_Boxarts folder. Returns Nothing when that name does not exist there.</summary>
    Public Shared Async Function DownloadBoxArtAsync(Repo As String, GameName As String, Client As HttpClient) As Task(Of Byte())
        Dim Url As String = BoxArtBase + Repo + "/master/Named_Boxarts/" + Uri.EscapeDataString(GameName) + ".png"
        Using Response As HttpResponseMessage = Await Client.GetAsync(Url).ConfigureAwait(False)
            If Response.StatusCode = HttpStatusCode.NotFound Then Return Nothing
            Response.EnsureSuccessStatusCode()
            Return Await Response.Content.ReadAsByteArrayAsync().ConfigureAwait(False)
        End Using
    End Function

    ''' <summary>
    ''' Serial -> game name of the Redump database of the platform. The file is cached in the covers folder and
    ''' downloaded again when it is older than 30 days (the old copy is used when the download fails).
    ''' </summary>
    Public Shared Async Function LoadSerialNamesAsync(PlatformName As String, DatFileName As String, Client As HttpClient, Optional CacheDirectory As String = Nothing) As Task(Of Dictionary(Of String, String))
        Dim Folder As String = If(CacheDirectory, CoverArt.CoversDirectory)
        Dim DatFile As String = Path.Combine(Folder, "redump-" + PlatformName + ".dat")
        Dim IsFresh As Boolean = File.Exists(DatFile) AndAlso (DateTime.Now - File.GetLastWriteTime(DatFile)).TotalDays < DatCacheDays
        If Not IsFresh Then
            Try
                Dim Content As String = Await Client.GetStringAsync(DatBase + Uri.EscapeDataString(DatFileName)).ConfigureAwait(False)
                Directory.CreateDirectory(Folder)
                File.WriteAllText(DatFile, Content)
            Catch ex As Exception
                If Not File.Exists(DatFile) Then Throw
            End Try
        End If
        Return ParseSerialNames(File.ReadAllText(DatFile))
    End Function

    ''' <summary>
    ''' Reads the game names of a Redump DAT file: serial -> the first game name that has it.
    ''' A game has a line <c>name "..."</c> and a line <c>serial "..."</c>, both indented with a tab.
    ''' </summary>
    Public Shared Function ParseSerialNames(DatContent As String) As Dictionary(Of String, String)
        Dim Names As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim CurrentName As String = Nothing
        For Each RawLine As String In DatContent.Split({vbLf}, StringSplitOptions.None)
            Dim Line As String = RawLine.TrimEnd(vbCr)
            If Line.StartsWith(vbTab + "name """) AndAlso Line.EndsWith("""") Then
                CurrentName = Line.Substring(7, Line.Length - 8)
            ElseIf Line.StartsWith(vbTab + "serial """) AndAlso Line.EndsWith("""") AndAlso CurrentName IsNot Nothing Then
                Dim Serial As String = Line.Substring(9, Line.Length - 10)
                If Not Names.ContainsKey(Serial) Then Names(Serial) = CurrentName
            End If
        Next
        Return Names
    End Function

    ''' <summary>
    ''' Names to try in libretro for a Redump name, most exact first: without the disc/track part, then without the
    ''' trailing groups one at a time ("Armored Core (USA) (Rerelease)" -> "Armored Core (USA)"), then with only the
    ''' first region ("(USA, Canada)" -> "(USA)").
    ''' </summary>
    Public Shared Function LibretroNameCandidates(RedumpName As String) As List(Of String)
        Dim Result As New List(Of String)
        Dim Base As String = Regex.Replace(RedumpName, "\s*\((Disc|Disk|Track)[^)]*\)", "").Trim()
        AddUnique(Result, Base)

        Dim Current As String = Base
        Do
            Dim Match As Match = Regex.Match(Current, "^(.*)\s+\([^()]*\)$")
            If Not Match.Success Then Exit Do
            Current = Match.Groups(1).Value.Trim()
            If Not Current.Contains("(") Then Exit Do
            AddUnique(Result, Current)
        Loop

        For Each Name As String In Result.ToList()
            AddUnique(Result, Regex.Replace(Name, "\(([^,()]+), [^()]*\)", "($1)"))
        Next
        Return Result
    End Function

    ''' <summary>Names to try in libretro for a typed title (the common region tags), at most five.</summary>
    Public Shared Function TitleNameCandidates(Title As String) As List(Of String)
        Dim Result As New List(Of String)
        If String.IsNullOrWhiteSpace(Title) Then Return Result
        Dim Clean As String = Regex.Replace(Title.Replace(":", " -"), " {2,}", " ").Trim()
        For Each Suffix As String In {" (USA)", " (Europe)", " (Japan)", " (World)", ""}
            AddUnique(Result, (Clean + Suffix).Trim())
        Next
        Return Result
    End Function

    Private Shared Sub AddUnique(Names As List(Of String), Name As String)
        If Name <> "" AndAlso Not Names.Contains(Name) Then Names.Add(Name)
    End Sub

End Class
