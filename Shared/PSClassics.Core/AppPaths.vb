Imports System.IO

''' <summary>
''' Central place for the folders the application depends on.
''' Paths are resolved from the folder of the executable instead of the process working directory, so the app keeps
''' working when it is started from a shortcut, a different "Start in" folder or a file association.
''' </summary>
Public Class AppPaths

    Private Shared _BaseDirectory As String = Normalize(AppContext.BaseDirectory)
    Private Shared _CacheDirectory As String = Nothing

    ''' <summary>
    ''' Folder that contains the running executable (no trailing separator).
    ''' Can be reassigned (for example by automated tests) to point to another folder that contains a "Tools" directory.
    ''' </summary>
    Public Shared Property BaseDirectory As String
        Get
            Return _BaseDirectory
        End Get
        Set(value As String)
            _BaseDirectory = Normalize(value)
        End Set
    End Property

    ''' <summary>The "Tools" folder with the third-party tools, emulators and game databases.</summary>
    Public Shared ReadOnly Property ToolsDirectory As String
        Get
            Return Path.Combine(_BaseDirectory, "Tools")
        End Get
    End Property

    ''' <summary>Per-user folder for data that must not live next to the program (Wine prefix, cache on macOS).</summary>
    Public Shared ReadOnly Property DataDirectory As String
        Get
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "PSClassicsfPKGBuilder")
        End Get
    End Property

    ''' <summary>
    ''' The "Cache" folder where the fPKG projects are assembled: next to the program, except on macOS where the
    ''' application bundle must not be modified (it is then in <see cref="DataDirectory"/>). Can be reassigned.
    ''' </summary>
    Public Shared Property CacheDirectory As String
        Get
            If _CacheDirectory IsNot Nothing Then Return _CacheDirectory
            If Platform.IsMacOS Then Return Path.Combine(DataDirectory, "Cache")
            Return Path.Combine(_BaseDirectory, "Cache")
        End Get
        Set(value As String)
            _CacheDirectory = If(value Is Nothing, Nothing, Normalize(value))
        End Set
    End Property

    ''' <summary>True when the Tools folder with the PS4 publishing tools and emulators exists.</summary>
    Public Shared Function ToolsAvailable() As Boolean
        Return File.Exists(Path.Combine(ToolsDirectory, "PS4", "gengp4_patch.exe")) AndAlso Directory.Exists(Path.Combine(ToolsDirectory, "PS4", "emus"))
    End Function

    Private Shared Function Normalize(Value As String) As String
        Dim Trimmed As String = Value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Return If(Trimmed = "", Value, Trimmed)
    End Function

End Class
