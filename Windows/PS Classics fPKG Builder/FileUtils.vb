Imports System.IO

''' <summary>Small file system helpers that replace the VB "My.Computer.FileSystem" members (not available in WPF apps on modern .NET).</summary>
Public Class FileUtils

    ''' <summary>Recursively copies a directory. The destination is created when it does not exist.</summary>
    Public Shared Sub CopyDirectory(SourceDirectory As String, DestinationDirectory As String, Overwrite As Boolean)
        If Not Directory.Exists(SourceDirectory) Then
            Throw New DirectoryNotFoundException("Source directory not found: " + SourceDirectory)
        End If

        Directory.CreateDirectory(DestinationDirectory)

        For Each SourceFile As String In Directory.GetFiles(SourceDirectory)
            File.Copy(SourceFile, Path.Combine(DestinationDirectory, Path.GetFileName(SourceFile)), Overwrite)
        Next

        For Each SourceSubDirectory As String In Directory.GetDirectories(SourceDirectory)
            CopyDirectory(SourceSubDirectory, Path.Combine(DestinationDirectory, Path.GetFileName(SourceSubDirectory)), Overwrite)
        Next
    End Sub

End Class
