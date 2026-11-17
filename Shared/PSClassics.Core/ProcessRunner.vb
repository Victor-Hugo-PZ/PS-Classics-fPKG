Imports System.IO

Public Class ProcessResult
    Public Property ExitCode As Integer
    ''' <summary>Everything the process wrote to the standard output.</summary>
    Public Property Output As String = ""
    ''' <summary>Everything the process wrote to the standard error.</summary>
    Public Property ErrorOutput As String = ""
End Class

''' <summary>Runs external programs (without a console window) and captures their output.</summary>
Public Class ProcessRunner

    ''' <summary>
    ''' Starts a process and waits for it to exit. The output is read before waiting for the exit
    ''' (and the error output in parallel), otherwise a full pipe buffer would block the child process forever.
    ''' </summary>
    Public Shared Function Run(FileName As String, Arguments As String, Optional WorkingDirectory As String = Nothing, Optional Environment As IDictionary(Of String, String) = Nothing) As ProcessResult
        Using NewProcess As New Process()
            NewProcess.StartInfo.FileName = FileName
            NewProcess.StartInfo.Arguments = Arguments
            NewProcess.StartInfo.UseShellExecute = False
            NewProcess.StartInfo.CreateNoWindow = True
            NewProcess.StartInfo.RedirectStandardOutput = True
            NewProcess.StartInfo.RedirectStandardError = True
            If Not String.IsNullOrEmpty(WorkingDirectory) Then
                NewProcess.StartInfo.WorkingDirectory = WorkingDirectory
            End If
            If Environment IsNot Nothing Then
                For Each Variable In Environment
                    NewProcess.StartInfo.Environment(Variable.Key) = Variable.Value
                Next
            End If

            NewProcess.Start()
            Dim ErrorOutputTask As Task(Of String) = NewProcess.StandardError.ReadToEndAsync()
            Dim Output As String = NewProcess.StandardOutput.ReadToEnd()
            NewProcess.WaitForExit()

            Return New ProcessResult With {.ExitCode = NewProcess.ExitCode, .Output = Output, .ErrorOutput = ErrorOutputTask.Result}
        End Using
    End Function

End Class

''' <summary>Wrappers for the PS4 publishing tools (Windows programs, run through Wine on Linux and macOS).</summary>
Public Class PkgTools

    Public Const SuccessMarker As String = "Create image Process finished with warning"

    Public Shared ReadOnly Property GenGp4Executable As String
        Get
            Return Path.Combine(AppPaths.ToolsDirectory, "PS4", "gengp4_patch.exe")
        End Get
    End Property

    Public Shared ReadOnly Property PubCmdExecutable As String
        Get
            Return Path.Combine(AppPaths.ToolsDirectory, "PS4", "mod-pub", "orbis-pub-cmd-3.38.exe")
        End Get
    End Property

    ''' <summary>Generates the GP4 project of a prepared cache folder (the ".gp4" file is written next to the folder).</summary>
    Public Shared Function GenerateGp4(GameCacheDirectory As String) As ProcessResult
        Return ToolRunner.Run(GenGp4Executable, {Platform.ToolPath(GameCacheDirectory)})
    End Function

    ''' <summary>Builds the fake package of a GP4 project into the output folder.</summary>
    Public Shared Function CreatePKG(Gp4File As String, OutputFolder As String) As ProcessResult
        Return ToolRunner.Run(PubCmdExecutable, {"img_create", "--oformat", "pkg", "--skip_digest", "--no_progress_bar", Platform.ToolPath(Gp4File), Platform.ToolPath(OutputFolder)})
    End Function

    ''' <summary>The publishing tool reports a successful build with this marker in its output.</summary>
    Public Shared Function IsSuccessful(Output As String) As Boolean
        Return Output IsNot Nothing AndAlso Output.Contains(SuccessMarker)
    End Function

    ''' <summary>Removes the cache folder of a previous build and creates an empty one.</summary>
    Public Shared Sub PrepareCacheDirectory(GameCacheDirectory As String, Gp4File As String)
        If Directory.Exists(GameCacheDirectory) Then
            Directory.Delete(GameCacheDirectory, True)
        End If
        If File.Exists(Gp4File) Then
            File.Delete(Gp4File)
        End If
        Directory.CreateDirectory(GameCacheDirectory)
    End Sub

End Class
