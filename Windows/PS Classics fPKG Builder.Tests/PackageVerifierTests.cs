using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class PackageVerifierParsingTests
{
    // Excerpt of a real "img_verify" output of a package made by this app.
    private const string Sample =
        "[Debug]\t2026-11-17 10:21:02 Verify Process started.\r\n" +
        "[Debug]\tparam.sfo: Param CATEGORY = gd.\r\n" +
        "[Error]\tThe following file is mandatory, but not found. (pronunciation.xml)\r\n" +
        "[Error]\tThe following file is mandatory, but not found. (icon0.png)\r\n" +
        "[Error]\tThe file/directory must not exist. (changeinfo)\r\n" +
        "[Warn]\tThe nptitle.dat file was not found, make sure to include it if the application uses NP features.\r\n" +
        "[Warn]\tSomething unexpected happened.\r\n" +
        "[Error]\tFormat of the SELF file is not valid. (eboot.bin, sceversion format)\r\n" +
        "[Error]\tFormat of the pkg file is not valid. (test package, not for submission)\r\n" +
        "[Error]\tNumber of Error(s) : 5\r\n" +
        "[Warn]\tNumber of Warning(s) : 3\r\n" +
        "[Error]\tResult : ERROR\r\n" +
        "[Error]\t2026-11-17 10:21:13 Verify Process finished with error(s).\r\n";

    [Fact]
    public void ParseVerifyOutput_KeepsErrorsAndWarningsAndDropsTheToolSummary()
    {
        var issues = PackageVerifier.ParseVerifyOutput(Sample);

        Assert.Equal(7, issues.Count);
        Assert.DoesNotContain(issues, i => i.Message.StartsWith("Number of") || i.Message.StartsWith("Result"));
        Assert.Equal(5, issues.Count(i => i.Severity == IssueSeverity.Error));
        Assert.Equal(2, issues.Count(i => i.Severity == IssueSeverity.Warning));
    }

    [Fact]
    public void ParseVerifyOutput_SeparatesCertificationChecksFromRealProblems()
    {
        var issues = PackageVerifier.ParseVerifyOutput(Sample);

        var problems = issues.Where(i => i.Severity == IssueSeverity.Error && !i.IsSubmissionCheck).Select(i => i.Message).ToList();
        Assert.Single(problems);
        Assert.Contains("icon0.png", problems[0]);

        Assert.Contains(issues, i => i.IsSubmissionCheck && i.Message.Contains("pronunciation.xml"));
        Assert.Contains(issues, i => i.IsSubmissionCheck && i.Message.Contains("changeinfo"));
        Assert.Contains(issues, i => i.IsSubmissionCheck && i.Message.Contains("sceversion"));
        Assert.Contains(issues, i => !i.IsSubmissionCheck && i.Severity == IssueSeverity.Warning && i.Message.Contains("unexpected"));
    }

    [Theory]
    [InlineData("The following file is mandatory, but not found. (pronunciation.xml)", true)]
    [InlineData("The file/directory must not exist. (npbind.dat)", true)]
    [InlineData("Format of the pkg file is not valid. (test package, not for submission)", true)]
    [InlineData("The following file is mandatory, but not found. (icon0.png)", false)]
    [InlineData("The following file is mandatory, but not found. (pic1.png)", false)]
    [InlineData("Could not open or read image file.", false)]
    public void IsSubmissionCheck_RecognizesSonyCertificationRules(string message, bool expected)
    {
        Assert.Equal(expected, PackageVerifier.IsSubmissionCheck(message));
    }

    [Fact]
    public void ParseVerifyOutput_HandlesEmptyInput()
    {
        Assert.Empty(PackageVerifier.ParseVerifyOutput(null));
        Assert.Empty(PackageVerifier.ParseVerifyOutput(""));
    }

    [Fact]
    public void Report_IsHealthyOnlyWithoutRealProblems()
    {
        var report = new PackageReport();
        report.Issues.AddRange(PackageVerifier.ParseVerifyOutput("[Error]\tThe following file is mandatory, but not found. (pronunciation.xml)\r\n"));
        Assert.True(report.IsHealthy);
        Assert.Contains("no problems", report.ToText(), StringComparison.OrdinalIgnoreCase);

        report.Issues.AddRange(PackageVerifier.ParseVerifyOutput("[Error]\tThe following file is mandatory, but not found. (icon0.png)\r\n"));
        Assert.False(report.IsHealthy);
        Assert.Contains("PROBLEMS", report.ToText());
    }

    [Fact]
    public void CheckStructure_ReportsFilesThatAreNotPackages()
    {
        using var ws = new TempWorkspace();
        var garbage = ws.Write("garbage.pkg", new byte[4096]);
        var issues = PackageVerifier.CheckStructure(garbage);
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Verify_ThrowsForMissingFiles()
    {
        using var ws = new TempWorkspace();
        Assert.Throws<FileNotFoundException>(() => PackageVerifier.Verify(Path.Combine(ws.Root, "missing.pkg")));
        var pkg = ws.Write("x.pkg", new byte[16]);
        Assert.Throws<FileNotFoundException>(() => PackageVerifier.Verify(pkg)); // no Tools folder in the workspace
    }
}

/// <summary>Builds real packages and checks them with the SDK checker. Skipped without the Tools folder.</summary>
public class PackageVerifierIntegrationTests
{
    private static byte[] Noise(int length, int seed = 1)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static void Iso(string path, params (string Path, byte[] Data)[] files)
    {
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "TEST" };
        foreach (var (name, data) in files) builder.AddFile(name, data);
        builder.Build(path);
    }

    private static string BuildPS1(TempWorkspace ws)
    {
        var bin = ws.Write(@"games\ps1.bin", new byte[2352 * 300]);
        ws.Write(@"games\ps1.cue", "FILE \"ps1.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        PS1PackageBuilder.Build(new PS1BuildRequest { Title = "T", NPTitle = "SLUS00594", DetectedGameID = "SLUS-00594", Disc1 = bin }, output);
        return Directory.GetFiles(output, "*.pkg").Single();
    }

    [RequiresToolsFact]
    public void PS1Package_HasNoProblemsBesidesSonyCertificationRules()
    {
        using var ws = new TempWorkspace(linkTools: true);

        var report = PackageVerifier.Verify(BuildPS1(ws));

        Assert.True(report.IsHealthy, report.ToText());
        Assert.Contains("SLUS00594", report.Info);
        Assert.NotEmpty(report.SubmissionIssues); // pronunciation.xml etc. are always reported for fake packages
    }

    [RequiresToolsFact]
    public void PS2Package_HasNoProblemsBesidesSonyCertificationRules()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = Path.Combine(ws.Root, "ps2.iso");
        Iso(iso, ("SLUS_212.58", Noise(8192)));
        var emulator = Directory.GetDirectories(Path.Combine(TestEnvironment.ToolsDirectory, "PS4", "emus"))
            .Select(Path.GetFileName).First(n => n != "ps1hd" && n != "psphd");
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        PS2PackageBuilder.Build(new PS2BuildRequest { Title = "T", NPTitle = "SLUS21258", DetectedGameID = "SLUS_212.58", Disc1 = iso, Emulator = emulator, Uprender = "2x2", Upscaling = "None", DisplayMode = "Normal" }, output);

        var report = PackageVerifier.Verify(Directory.GetFiles(output, "*.pkg").Single());

        Assert.True(report.IsHealthy, report.ToText());
    }

    [RequiresToolsFact]
    public void TruncatedPackage_IsReportedAsDamaged()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var pkg = BuildPS1(ws);
        using (var fs = new FileStream(pkg, FileMode.Open, FileAccess.Write))
            fs.SetLength(new FileInfo(pkg).Length / 2);

        // The structural check alone is enough (and instant); the SDK checker is slow on large files.
        var issues = PackageVerifier.CheckStructure(pkg);

        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && (i.Message.Contains("incomplete or damaged") || i.Message.Contains("could not be read")));
    }

    [RequiresToolsFact]
    public void PSPPackage_WithoutIcon_IsReportedAsProblem()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = Path.Combine(ws.Root, "psp.iso");
        Iso(iso, (@"PSP_GAME\PARAM.SFO", Noise(512)), ("UMD_DATA.BIN", System.Text.Encoding.ASCII.GetBytes("ULUS-10041|0123456789")), (@"PSP_GAME\SYSDIR\EBOOT.BIN", Noise(4096, 3)));
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        PSPPackageBuilder.Build(new PSPBuildRequest { Title = "T", NPTitle = "ULUS10041", IsoFile = iso, Warn = _ => { } }, output);

        var report = PackageVerifier.Verify(Directory.GetFiles(output, "*.pkg").Single());

        Assert.False(report.IsHealthy);
        Assert.Contains(report.ProblemIssues, i => i.Message.Contains("icon0.png"));
    }
}
