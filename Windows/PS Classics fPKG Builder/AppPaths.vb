Imports System.IO

''' <summary>
''' Central place for the folders the application depends on.
''' Paths are resolved from the folder of the executable instead of the process
''' working directory, so the app keeps working when it is started from a
''' shortcut, a different "Start in" folder or a file association.
''' </summary>
Public Class AppPaths

    ''' <summary>Folder that contains the running executable (no trailing separator).</summary>
    Public Shared ReadOnly Property BaseDirectory As String = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

End Class
