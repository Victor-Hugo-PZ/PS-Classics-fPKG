Imports System.IO
Imports System.Reflection
Imports PS_Classics_fPKG_Builder

''' <summary>
''' PkgCheck: checks a PS4 fake package (.pkg) made by PS Classics fPKG Builder.
''' Exit codes: 0 = no problems, 1 = problems found, 2 = the check could not run (bad arguments, missing file or tools).
''' </summary>
Public Module Program

    Private Const Usage As String =
        "PkgCheck - checks a PS4 fake package (.pkg)" + vbLf +
        vbLf +
        "Usage:" + vbLf +
        "  PkgCheck <file.pkg> [options]" + vbLf +
        vbLf +
        "Options:" + vbLf +
        "  --quick          Only the structural checks (instant). Skips the SDK checker, which can take" + vbLf +
        "                   several minutes on large games, and does not need the Tools folder." + vbLf +
        "  --tool-output    Also print the complete output of the SDK checker." + vbLf +
        "  --tools <dir>    Folder that contains the 'Tools' folder (default: next to PkgCheck.exe)." + vbLf +
        "  -h, --help       Show this help." + vbLf +
        vbLf +
        "Exit code: 0 = no problems, 1 = problems found, 2 = the check could not run." + vbLf +
        "Sony certification rules that every fake package fails are listed separately and are safe to ignore."

    Function Main(Args As String()) As Integer
        Console.OutputEncoding = Text.Encoding.UTF8

        Dim PkgFile As String = Nothing
        Dim Quick As Boolean = False
        Dim ShowToolOutput As Boolean = False
        Dim ToolsBase As String = Nothing

        Dim Index As Integer = 0
        While Index < Args.Length
            Select Case Args(Index).ToLowerInvariant()
                Case "-h", "--help", "/?"
                    Console.WriteLine(Usage)
                    Return 0
                Case "--quick"
                    Quick = True
                Case "--tool-output"
                    ShowToolOutput = True
                Case "--tools"
                    Index += 1
                    If Index >= Args.Length Then
                        Console.Error.WriteLine("Missing folder after --tools.")
                        Return 2
                    End If
                    ToolsBase = Args(Index)
                Case Else
                    If Args(Index).StartsWith("-", StringComparison.Ordinal) Then
                        Console.Error.WriteLine("Unknown option: " + Args(Index))
                        Console.Error.WriteLine()
                        Console.Error.WriteLine(Usage)
                        Return 2
                    End If
                    If PkgFile IsNot Nothing Then
                        Console.Error.WriteLine("Only one package can be checked at a time.")
                        Return 2
                    End If
                    PkgFile = Args(Index)
            End Select
            Index += 1
        End While

        If PkgFile Is Nothing Then
            Console.Error.WriteLine(Usage)
            Return 2
        End If

        'Tools: given by the user, or next to the executable, or in a parent folder (running from the source tree)
        AppPaths.BaseDirectory = If(ToolsBase, FindToolsBase())

        Try
            Dim Report As PackageReport = PackageVerifier.Verify(PkgFile, Not Quick)
            Console.WriteLine(Report.ToText())
            If ShowToolOutput AndAlso Report.ToolOutput <> "" Then
                Console.WriteLine("---- SDK checker output ----")
                Console.WriteLine(Report.ToolOutput)
            End If
            Return If(Report.IsHealthy, 0, 1)
        Catch ex As FileNotFoundException
            Console.Error.WriteLine(ex.Message + If(String.IsNullOrEmpty(ex.FileName), "", " (" + ex.FileName + ")"))
            Return 2
        Catch ex As Exception
            Console.Error.WriteLine("The package could not be checked: " + ex.Message)
            Return 2
        End Try
    End Function

    ''' <summary>Folder that contains "Tools": the executable folder or the first parent that has it.</summary>
    Private Function FindToolsBase() As String
        Dim Folder As New DirectoryInfo(AppContext.BaseDirectory)
        While Folder IsNot Nothing
            If Directory.Exists(Path.Combine(Folder.FullName, "Tools", "PS4")) Then Return Folder.FullName
            Folder = Folder.Parent
        End While
        Return AppContext.BaseDirectory
    End Function

End Module
