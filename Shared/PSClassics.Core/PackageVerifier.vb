Imports System.IO
Imports System.Text
Imports LibOrbisPkg.PKG

Public Enum IssueSeverity
    Info
    Warning
    [Error]
End Enum

''' <summary>One finding of the package check.</summary>
Public Class PackageIssue
    Public Property Severity As IssueSeverity
    Public Property Message As String = ""
    ''' <summary>
    ''' True for Sony certification ("submission") checks. They always fail for fake packages and do not
    ''' affect installing the package on a jailbroken console, so they are reported separately.
    ''' </summary>
    Public Property IsSubmissionCheck As Boolean
End Class

''' <summary>Result of <see cref="PackageVerifier.Verify"/>.</summary>
Public Class PackageReport
    Public Property PackagePath As String = ""
    ''' <summary>Summary of the package (content ID, title ID, SDK version...), as printed by "img_info".</summary>
    Public Property Info As String = ""
    Public Property Issues As New List(Of PackageIssue)
    ''' <summary>Complete output of the Sony checker, for support.</summary>
    Public Property ToolOutput As String = ""

    ''' <summary>Errors that are not Sony certification checks: the ones worth looking at.</summary>
    Public ReadOnly Property ProblemIssues As IEnumerable(Of PackageIssue)
        Get
            Return Issues.Where(Function(Issue) Issue.Severity = IssueSeverity.Error AndAlso Not Issue.IsSubmissionCheck)
        End Get
    End Property

    Public ReadOnly Property SubmissionIssues As IEnumerable(Of PackageIssue)
        Get
            Return Issues.Where(Function(Issue) Issue.IsSubmissionCheck)
        End Get
    End Property

    ''' <summary>True when no problem other than the expected Sony certification checks was found.</summary>
    Public ReadOnly Property IsHealthy As Boolean
        Get
            Return Not ProblemIssues.Any()
        End Get
    End Property

    Public Function ToText() As String
        Dim Text As New StringBuilder()
        Text.AppendLine(Localizer.F("Package: {0}", PackagePath))
        Text.AppendLine(If(IsHealthy, Localizer.T("Result: no problems found."), Localizer.F("Result: {0} problem(s) found.", ProblemIssues.Count())))
        Text.AppendLine()

        If ProblemIssues.Any() Then
            Text.AppendLine(Localizer.T("PROBLEMS (may stop the package from installing or running)"))
            For Each Issue In ProblemIssues
                Text.AppendLine("  - " + Issue.Message)
            Next
            Text.AppendLine()
        End If

        Dim Warnings As List(Of PackageIssue) = Issues.Where(Function(Issue) Issue.Severity = IssueSeverity.Warning AndAlso Not Issue.IsSubmissionCheck).ToList()
        If Warnings.Any() Then
            Text.AppendLine(Localizer.T("WARNINGS"))
            For Each Issue In Warnings
                Text.AppendLine("  - " + Issue.Message)
            Next
            Text.AppendLine()
        End If

        If SubmissionIssues.Any() Then
            Text.AppendLine(Localizer.T("EXPECTED FOR FAKE PACKAGES (Sony certification checks, safe to ignore)"))
            For Each Issue In SubmissionIssues
                Text.AppendLine("  - " + Issue.Message)
            Next
            Text.AppendLine()
        End If

        If Info <> "" Then
            Text.AppendLine(Localizer.T("PACKAGE INFO"))
            Text.AppendLine(Info.TrimEnd())
        End If
        Return Text.ToString()
    End Function
End Class

''' <summary>
''' Checks a generated .pkg with the checker of the PS4 publishing tools ("img_verify") plus a few structural checks
''' made with LibOrbisPkg, and separates real problems from the Sony certification rules that fake packages never meet.
''' </summary>
Public Class PackageVerifier

    ''' <summary>Passcode of fake packages (all zeros).</summary>
    Public Const FakePackagePasscode As String = "00000000000000000000000000000000"

    'Messages of the Sony checker that only matter for submitting a game to the PlayStation Store.
    Private Shared ReadOnly SubmissionPatterns As String() = {
        "pronunciation.xml",
        "(changeinfo)",
        "(npbind.dat)",
        "sceversion format",
        "test package, not for submission",
        "should support NEO mode",
        "does not contain a trophy pack",
        "nptitle.dat file was not found",
        "Number of Error(s)",
        "Number of Warning(s)",
        "Result : ",
        "Verify Process finished"
    }

    Public Shared Function IsSubmissionCheck(Message As String) As Boolean
        For Each Pattern As String In SubmissionPatterns
            If Message.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
        Next
        Return False
    End Function

    ''' <summary>Turns the "[Error]" and "[Warn]" lines of the checker output into issues. The summary lines of the tool are dropped.</summary>
    Public Shared Function ParseVerifyOutput(Output As String) As List(Of PackageIssue)
        Dim Issues As New List(Of PackageIssue)
        If Output Is Nothing Then Return Issues

        For Each RawLine As String In Output.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
            Dim Severity As IssueSeverity
            Dim Message As String

            If RawLine.StartsWith("[Error]", StringComparison.Ordinal) Then
                Severity = IssueSeverity.Error
                Message = RawLine.Substring("[Error]".Length).Trim()
            ElseIf RawLine.StartsWith("[Warn]", StringComparison.Ordinal) Then
                Severity = IssueSeverity.Warning
                Message = RawLine.Substring("[Warn]".Length).Trim()
            Else
                Continue For
            End If

            'Summary lines of the tool ("Number of Error(s)", "Result : ERROR", timestamps) are not findings
            If Message.StartsWith("Number of", StringComparison.Ordinal) OrElse Message.StartsWith("Result :", StringComparison.Ordinal) OrElse Message.Contains("Verify Process finished") Then Continue For

            Issues.Add(New PackageIssue With {.Severity = Severity, .Message = Message, .IsSubmissionCheck = IsSubmissionCheck(Message)})
        Next
        Return Issues
    End Function

    ''' <summary>Structural checks that do not need the SDK checker: the file is a PKG, its size matches the header and it has the files every package needs.</summary>
    Public Shared Function CheckStructure(PkgPath As String) As List(Of PackageIssue)
        Dim Issues As New List(Of PackageIssue)

        Try
            Using PkgStream As FileStream = File.OpenRead(PkgPath)
                Dim Pkg As Pkg = New PkgReader(PkgStream).ReadPkg()

                If Pkg.Header.package_size <> CULng(PkgStream.Length) Then
                    Issues.Add(New PackageIssue With {.Severity = IssueSeverity.Error, .Message = Localizer.F("The file size ({0} bytes) is different from the size in the package header ({1} bytes): the file is incomplete or damaged.", PkgStream.Length, Pkg.Header.package_size)})
                End If

                Dim EntryIds As HashSet(Of EntryId) = New HashSet(Of EntryId)(Pkg.Metas.Metas.Select(Function(Meta) Meta.id))
                If Not EntryIds.Contains(EntryId.PARAM_SFO) Then
                    Issues.Add(New PackageIssue With {.Severity = IssueSeverity.Error, .Message = Localizer.T("The package has no param.sfo.")})
                End If
                If Not EntryIds.Contains(EntryId.ICON0_PNG) Then
                    Issues.Add(New PackageIssue With {.Severity = IssueSeverity.Error, .Message = Localizer.T("The package has no icon0.png: the game tile will be empty or the console may refuse it.")})
                End If
            End Using
        Catch ex As Exception
            Issues.Add(New PackageIssue With {.Severity = IssueSeverity.Error, .Message = Localizer.F("The file could not be read as a PS4 package: {0}", ex.Message)})
        End Try

        Return Issues
    End Function

    ''' <summary>
    ''' Verifies a package. The SDK checker reads the whole package, so this can take a while for large games.
    ''' Throws FileNotFoundException when the file or the Tools folder does not exist.
    ''' </summary>
    Public Shared Function Verify(PkgPath As String, Optional RunSdkChecker As Boolean = True) As PackageReport
        If Not File.Exists(PkgPath) Then Throw New FileNotFoundException("The package was not found.", PkgPath)

        Dim Report As New PackageReport With {.PackagePath = PkgPath}

        'Quick mode: only the structural checks (instant, no Tools folder needed)
        If Not RunSdkChecker Then
            Report.Issues.AddRange(CheckStructure(PkgPath))
            Return Report
        End If

        Dim PubCmd As String = PkgTools.PubCmdExecutable
        If Not File.Exists(PubCmd) Then Throw New FileNotFoundException("The publishing tool was not found. Check the Tools folder.", PubCmd)

        Dim InfoResult As ProcessResult = ToolRunner.Run(PubCmd, {"img_info", Platform.ToolPath(PkgPath)})
        Report.Info = InfoResult.Output

        Dim VerifyResult As ProcessResult = ToolRunner.Run(PubCmd, {"img_verify", "--passcode", FakePackagePasscode, Platform.ToolPath(PkgPath)})
        Report.ToolOutput = VerifyResult.Output + VerifyResult.ErrorOutput

        Report.Issues.AddRange(CheckStructure(PkgPath))
        Report.Issues.AddRange(ParseVerifyOutput(Report.ToolOutput))

        'The tool could not run at all: do not report a clean package
        If Not Report.ToolOutput.Contains("Check started") AndAlso Not Report.Issues.Any(Function(Issue) Issue.Severity = IssueSeverity.Error) Then
            Report.Issues.Add(New PackageIssue With {.Severity = IssueSeverity.Error, .Message = Localizer.F("The SDK checker did not run: {0}", Report.ToolOutput.Trim())})
        End If
        Return Report
    End Function

End Class
