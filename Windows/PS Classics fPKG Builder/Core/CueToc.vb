Imports System.IO

''' <summary>Generates the TOC file the PS1 emulator needs for games with CDDA audio tracks.</summary>
Public Class CueToc

    ''' <summary>
    ''' Converts a CUE sheet with "cue2toc.exe" and returns the content of the generated TOC file.
    ''' cue2toc writes its output files (".TOC" and ".cu2") next to the CUE sheet, so the tool is run on a copy of the
    ''' CUE sheet in a temporary folder (the BIN size is passed explicitly). The original code left both files
    ''' in the folder of the user's game.
    ''' </summary>
    Public Shared Function Generate(CueFile As String, BinFile As String) As Byte()
        If Not File.Exists(CueFile) Then
            Throw New FileNotFoundException("The CUE file was not found. Every BIN file needs a CUE file with the same name next to it.", CueFile)
        End If
        If Not File.Exists(BinFile) Then
            Throw New FileNotFoundException("The BIN file was not found.", BinFile)
        End If

        Dim WorkDirectory As String = Path.Combine(Path.GetTempPath(), "psfpkg-cue2toc-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(WorkDirectory)
        Try
            Dim CueCopy As String = Path.Combine(WorkDirectory, Path.GetFileName(CueFile))
            File.Copy(CueFile, CueCopy)

            Dim Result As ProcessResult = ProcessRunner.Run(AppPaths.ToolsDirectory + "\cue2toc.exe",
                                                            """" + CueCopy + """ --size " + New FileInfo(BinFile).Length.ToString() + " --quiet",
                                                            WorkDirectory)

            Dim TocFiles As String() = Directory.GetFiles(WorkDirectory, "*.toc")
            If Result.ExitCode <> 0 OrElse TocFiles.Length <> 1 Then
                Throw New InvalidOperationException("The TOC file could not be created from '" + Path.GetFileName(CueFile) + "'." + vbCrLf + Result.ErrorOutput + Result.Output)
            End If

            Return File.ReadAllBytes(TocFiles(0))
        Finally
            Try
                Directory.Delete(WorkDirectory, True)
            Catch ex As IOException
                Debug.WriteLine("Could not remove the temporary folder " + WorkDirectory + ": " + ex.Message)
            End Try
        End Try
    End Function

End Class
