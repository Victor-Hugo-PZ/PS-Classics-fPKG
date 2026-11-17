Imports System.IO
Imports System.Runtime.InteropServices

''' <summary>Operating system helpers. The PS4 publishing tools are Windows programs: on Linux and macOS they run through Wine.</summary>
Public Class Platform

    Public Shared ReadOnly Property IsWindows As Boolean = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    Public Shared ReadOnly Property IsMacOS As Boolean = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
    Public Shared ReadOnly Property IsLinux As Boolean = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)

    ''' <summary>
    ''' The path as a Windows tool sees it: on Windows the full path; on Linux and macOS the Wine path
    ''' (Wine maps the Unix root to the Z: drive, so "/home/me/game.iso" becomes "Z:\home\me\game.iso").
    ''' </summary>
    Public Shared Function ToolPath(HostPath As String) As String
        Dim FullPath As String = Path.GetFullPath(HostPath)
        If FullPath.Length > 3 Then FullPath = FullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Return ToToolPath(FullPath, IsWindows)
    End Function

    ''' <summary>Conversion used by <see cref="ToolPath"/>, testable on every OS.</summary>
    Public Shared Function ToToolPath(FullPath As String, OnWindows As Boolean) As String
        If OnWindows Then Return FullPath
        Return "Z:" + FullPath.Replace("/"c, "\"c)
    End Function

End Class

''' <summary>Runs the Windows tools of the Tools folder, directly on Windows and through Wine on Linux and macOS.</summary>
Public Class ToolRunner

    ''' <summary>Quotes one command line argument for a Windows program.</summary>
    Public Shared Function Quote(Argument As String) As String
        If Argument Is Nothing Then Return """"""
        If Argument.Length > 0 AndAlso Argument.IndexOfAny({" "c, ControlChars.Tab, """"c}) < 0 Then Return Argument
        Return """" + Argument.Replace("""", "\""") + """"
    End Function

    ''' <summary>
    ''' Runs a Windows tool. <paramref name="Arguments"/> must already contain tool paths (see <see cref="Platform.ToolPath"/>)
    ''' for every file or folder. Throws <see cref="WineNotFoundException"/> on Linux/macOS when Wine is not installed.
    ''' </summary>
    Public Shared Function Run(ExePath As String, Arguments As IEnumerable(Of String), Optional WorkingDirectory As String = Nothing) As ProcessResult
        Dim ArgumentLine As String = String.Join(" ", Arguments.Select(AddressOf Quote))
        Dim FullExePath As String = Path.GetFullPath(ExePath)

        If Platform.IsWindows Then
            Return ProcessRunner.Run(FullExePath, ArgumentLine, WorkingDirectory)
        End If

        Dim WineExecutable As String = Wine.FindExecutable()
        If WineExecutable Is Nothing Then Throw New WineNotFoundException()
        Return ProcessRunner.Run(WineExecutable, Quote(FullExePath) + " " + ArgumentLine, WorkingDirectory, Wine.Environment())
    End Function

End Class

Public Class WineNotFoundException
    Inherits Exception

    Public Sub New()
        MyBase.New("Wine was not found. On Linux and macOS the PS4 publishing tools run through Wine: install it and set it up from the app (Wine setup).")
    End Sub
End Class

''' <summary>State of the Wine installation used on Linux and macOS.</summary>
Public Class WineStatus
    Public Property Executable As String
    Public Property PrefixReady As Boolean
    Public Property Vc2008Installed As Boolean
    Public Property WinetricksExecutable As String

    Public ReadOnly Property WineFound As Boolean
        Get
            Return Executable IsNot Nothing
        End Get
    End Property

    ''' <summary>True when everything needed to build packages is in place.</summary>
    Public ReadOnly Property IsReady As Boolean
        Get
            Return WineFound AndAlso PrefixReady AndAlso Vc2008Installed
        End Get
    End Property
End Class

''' <summary>Wine support for Linux and macOS: finds Wine, uses a dedicated prefix and installs the Visual C++ 2008 runtime the Sony tools need.</summary>
Public Class Wine

    ''' <summary>Path of the wine program to use instead of searching for it (settings, tests). Can also be set with the PSFPKG_WINE environment variable.</summary>
    Public Shared Property ExecutableOverride As String

    ''' <summary>Wine prefix of the app (kept apart from the user's own Wine setup).</summary>
    Public Shared ReadOnly Property PrefixDirectory As String
        Get
            Dim FromEnvironment As String = System.Environment.GetEnvironmentVariable("PSFPKG_WINEPREFIX")
            If Not String.IsNullOrEmpty(FromEnvironment) Then Return FromEnvironment
            Return Path.Combine(AppPaths.DataDirectory, "wine-prefix")
        End Get
    End Property

    ''' <summary>Candidate locations of the wine program, in order of preference.</summary>
    Public Shared Function CandidateExecutables() As List(Of String)
        Dim Candidates As New List(Of String)
        Dim FromEnvironment As String = System.Environment.GetEnvironmentVariable("PSFPKG_WINE")
        If Not String.IsNullOrEmpty(ExecutableOverride) Then Candidates.Add(ExecutableOverride)
        If Not String.IsNullOrEmpty(FromEnvironment) Then Candidates.Add(FromEnvironment)

        If Platform.IsMacOS Then
            'Wine Stable from Homebrew (brew install --cask wine-stable) or from the Gcenx builds
            Candidates.Add("/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine")
            Candidates.Add("/Applications/Wine Devel.app/Contents/Resources/wine/bin/wine")
            Candidates.Add("/Applications/Wine Staging.app/Contents/Resources/wine/bin/wine")
            Candidates.Add("/opt/homebrew/bin/wine")
            Candidates.Add("/usr/local/bin/wine")
        End If

        For Each Folder As String In (If(System.Environment.GetEnvironmentVariable("PATH"), "")).Split(Path.PathSeparator)
            If String.IsNullOrWhiteSpace(Folder) Then Continue For
            Candidates.Add(Path.Combine(Folder, "wine"))
            Candidates.Add(Path.Combine(Folder, "wine64"))
        Next
        Return Candidates
    End Function

    ''' <summary>The wine program, or Nothing when Wine is not installed.</summary>
    Public Shared Function FindExecutable() As String
        Return CandidateExecutables().FirstOrDefault(Function(Candidate) File.Exists(Candidate))
    End Function

    ''' <summary>The winetricks script, or Nothing.</summary>
    Public Shared Function FindWinetricks() As String
        Dim Candidates As New List(Of String) From {"/opt/homebrew/bin/winetricks", "/usr/local/bin/winetricks"}
        For Each Folder As String In (If(System.Environment.GetEnvironmentVariable("PATH"), "")).Split(Path.PathSeparator)
            If Not String.IsNullOrWhiteSpace(Folder) Then Candidates.Add(Path.Combine(Folder, "winetricks"))
        Next
        Return Candidates.FirstOrDefault(Function(Candidate) File.Exists(Candidate))
    End Function

    ''' <summary>Environment of every Wine call: dedicated prefix, no debug output, no Mono/Gecko install prompts.</summary>
    Public Shared Function Environment() As Dictionary(Of String, String)
        Dim Values As New Dictionary(Of String, String) From {
            {"WINEPREFIX", PrefixDirectory},
            {"WINEDEBUG", "-all"},
            {"WINEDLLOVERRIDES", "mscoree,mshtml="}
        }
        'winetricks finds wine through the WINE variable
        Dim WineExecutable As String = FindExecutable()
        If WineExecutable IsNot Nothing Then Values("WINE") = WineExecutable
        Return Values
    End Function

    Public Shared Function GetStatus() As WineStatus
        Dim Status As New WineStatus With {.Executable = FindExecutable(), .WinetricksExecutable = FindWinetricks()}
        Dim System32 As String = Path.Combine(PrefixDirectory, "drive_c", "windows", "system32")
        Status.PrefixReady = Directory.Exists(System32)
        Status.Vc2008Installed = Status.PrefixReady AndAlso IsVc2008Installed()
        Return Status
    End Function

    ''' <summary>The Visual C++ 2008 runtime (msvcr90.dll) installed by "winetricks vcrun2008" lives in the side-by-side folder of the prefix.</summary>
    Private Shared Function IsVc2008Installed() As Boolean
        Dim WinSxS As String = Path.Combine(PrefixDirectory, "drive_c", "windows", "winsxs")
        If Not Directory.Exists(WinSxS) Then Return False
        Try
            Return Directory.EnumerateFiles(WinSxS, "msvcr90.dll", SearchOption.AllDirectories).Any()
        Catch ex As IOException
            Return False
        Catch ex As UnauthorizedAccessException
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Creates the prefix and installs the Visual C++ 2008 runtime with winetricks (downloads it from Microsoft).
    ''' <paramref name="Log"/> receives progress messages. Returns True when Wine is ready afterwards.
    ''' </summary>
    Public Shared Function SetUp(Log As Action(Of String)) As Boolean
        Dim Status As WineStatus = GetStatus()
        If Not Status.WineFound Then
            Log?.Invoke(Localizer.T("Wine was not found."))
            Return False
        End If

        Directory.CreateDirectory(PrefixDirectory)
        If Not Status.PrefixReady Then
            Log?.Invoke(Localizer.T("Creating the Wine prefix (first run, this can take a minute)..."))
            Dim Boot As ProcessResult = ProcessRunner.Run(Status.Executable, "wineboot --init", Nothing, Environment())
            Log?.Invoke(Boot.Output + Boot.ErrorOutput)
        End If

        If Not GetStatus().Vc2008Installed Then
            If Status.WinetricksExecutable Is Nothing Then
                Log?.Invoke(Localizer.T("winetricks was not found: install it to set up the Visual C++ 2008 runtime."))
                Return False
            End If
            Log?.Invoke(Localizer.T("Installing the Visual C++ 2008 runtime with winetricks (downloads it from Microsoft)..."))
            Dim Tricks As ProcessResult = ProcessRunner.Run(Status.WinetricksExecutable, "-q vcrun2008", Nothing, Environment())
            Log?.Invoke(Tricks.Output + Tricks.ErrorOutput)
        End If

        Dim Final As WineStatus = GetStatus()
        Log?.Invoke(If(Final.IsReady, Localizer.T("Wine is ready."), Localizer.T("Wine is not ready yet: check the messages above.")))
        Return Final.IsReady
    End Function

    ''' <summary>Install instructions for the current OS, shown by the apps.</summary>
    Public Shared Function InstallInstructions() As String
        If Platform.IsMacOS Then
            Return Localizer.T("Install Wine and winetricks with Homebrew (https://brew.sh):") + vbLf +
                   "  brew install --cask --no-quarantine wine-stable" + vbLf +
                   "  brew install winetricks" + vbLf +
                   Localizer.T("Then press 'Set up Wine' in the app.")
        End If
        Return Localizer.T("Install Wine (64-bit, with 32-bit support) and winetricks from your distribution, for example:") + vbLf +
               "  Ubuntu/Mint/Debian: sudo dpkg --add-architecture i386 && sudo apt update && sudo apt install wine wine32:i386 winetricks" + vbLf +
               "  Fedora: sudo dnf install wine winetricks" + vbLf +
               "  Arch: sudo pacman -S wine winetricks" + vbLf +
               Localizer.T("Then press 'Set up Wine' in the app.")
    End Function

End Class
