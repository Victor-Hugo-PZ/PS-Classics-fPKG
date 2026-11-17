using Xunit;

namespace PSClassics.Tests;

/// <summary>The PkgCheck command line tool (its Main is called directly; console output is captured).</summary>
public class PkgCheckTests
{
    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var oldOut = Console.Out;
        var oldErr = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var code = PkgCheck.Program.Main(args);
            return (code, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldErr);
        }
    }

    [Fact]
    public void Help_ShowsUsageAndExitsWithZero()
    {
        var (code, output, _) = Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("PkgCheck <file.pkg>", output);
        Assert.Contains("--quick", output);
    }

    [Fact]
    public void WithoutArguments_ShowsUsageAndExitsWithTwo()
    {
        var (code, _, error) = Run();
        Assert.Equal(2, code);
        Assert.Contains("Usage:", error);
    }

    [Fact]
    public void UnknownOption_ExitsWithTwo()
    {
        var (code, _, error) = Run("game.pkg", "--bogus");
        Assert.Equal(2, code);
        Assert.Contains("Unknown option", error);
    }

    [Fact]
    public void MoreThanOnePackage_ExitsWithTwo()
    {
        var (code, _, error) = Run("a.pkg", "b.pkg");
        Assert.Equal(2, code);
        Assert.Contains("one package", error);
    }

    [Fact]
    public void MissingFile_ExitsWithTwo()
    {
        using var ws = new TempWorkspace();
        var (code, _, error) = Run(Path.Combine(ws.Root, "missing.pkg"), "--quick");
        Assert.Equal(2, code);
        Assert.Contains("not found", error);
    }

    [Fact]
    public void QuickCheck_OfAFileThatIsNotAPackage_ExitsWithOneAndExplainsWhy()
    {
        using var ws = new TempWorkspace();
        var garbage = ws.Write("garbage.pkg", new byte[4096]);

        var (code, output, _) = Run(garbage, "--quick");

        Assert.Equal(1, code);
        Assert.Contains("PROBLEMS", output);
        Assert.Contains("could not be read", output);
    }

    [RequiresToolsFact]
    public void QuickCheck_OfAGeneratedPackage_ExitsWithZero()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = ws.Write(@"games\ps1.bin", new byte[2352 * 300]);
        ws.Write(@"games\ps1.cue", "FILE \"ps1.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        PS_Classics_fPKG_Builder.PS1PackageBuilder.Build(new PS_Classics_fPKG_Builder.PS1BuildRequest { Title = "T", NPTitle = "SLUS00594", DetectedGameID = "SLUS-00594", Disc1 = bin }, output);
        var pkg = Directory.GetFiles(output, "*.pkg").Single();

        var (code, text, _) = Run(pkg, "--quick");

        Assert.Equal(0, code);
        Assert.Contains("no problems", text, StringComparison.OrdinalIgnoreCase);
    }
}
