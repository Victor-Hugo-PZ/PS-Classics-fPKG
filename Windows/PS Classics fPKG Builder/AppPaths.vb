Imports System.IO

''' <summary>
''' Central place for the folders the application depends on.
''' Paths are resolved from the folder of the executable instead of the process
''' working directory, so the app keeps working when it is started from a
''' shortcut, a different "Start in" folder or a file association.
''' </summary>
Public Class AppPaths

    Private Shared _BaseDirectory As String = Normalize(AppContext.BaseDirectory)

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
            Return _BaseDirectory + "\Tools"
        End Get
    End Property

    ''' <summary>The "Cache" folder where the fPKG projects are assembled.</summary>
    Public Shared ReadOnly Property CacheDirectory As String
        Get
            Return _BaseDirectory + "\Cache"
        End Get
    End Property

    Private Shared Function Normalize(Path As String) As String
        Return Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
    End Function

End Class
