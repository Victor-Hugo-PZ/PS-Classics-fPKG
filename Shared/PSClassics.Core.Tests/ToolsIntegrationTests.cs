using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>
/// Integration tests: game ID and CRC reading (every OS) and complete builds with the real tools (cue2toc, gengp4,
/// orbis-pub-cmd, pspdecrypt), which run directly on Windows and through Wine on Linux/macOS. Builds are skipped when they cannot run.
/// </summary>
public class ToolsIntegrationTests
{
    private static string CreateIso(string path, params (string Path, byte[] Data)[] files)
    {
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "TEST" };
        foreach (var (name, data) in files)
            builder.AddFile(name, data);
        builder.Build(path);
        return path;
    }

    private static byte[] Noise(int length, int seed = 1)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    /// <summary>A tiny fake PS1 disc: a BIN with a SYSTEM.CNF-like BOOT line and a matching CUE sheet.</summary>
    private static string CreatePS1Disc(TempWorkspace ws, string name = "game")
    {
        var bin = new byte[2352 * 300];
        var boot = System.Text.Encoding.ASCII.GetBytes("BOOT = cdrom:\\SLUS_005.94;1\r\nTCB = 4\r\n");
        Array.Copy(boot, 0, bin, 4096, boot.Length);
        var binPath = ws.Write($@"games\{name}.bin", bin);
        ws.Write($@"games\{name}.cue", $"FILE \"{name}.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        return binPath;
    }

    // Game IDs and the CRC are read without external programs, so these run on every OS.

    [Fact]
    public void PS1_GameIdIsReadFromTheBin()
    {
        using var ws = new TempWorkspace();
        var bin = CreatePS1Disc(ws);

        Assert.Equal("SLUS-00594", PS1Game.GetPS1GameID(bin));
    }

    [Fact]
    public void PS1_GameIdIsEmptyWhenTheBinHasNoBootEntry()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write(@"games\empty.bin", new byte[2352 * 10]);

        Assert.Equal("", PS1Game.GetPS1GameID(bin));
    }

    [Fact]
    public void PS1_GameIdIsOnlySearchedInTheFirstSevenMegabytes()
    {
        using var ws = new TempWorkspace();
        var bin = new byte[PS1Game.ScanLength + 4096];
        var boot = System.Text.Encoding.ASCII.GetBytes("BOOT = cdrom:\\SLUS_005.94;1\r\n");
        Array.Copy(boot, 0, bin, PS1Game.ScanLength + 100, boot.Length); // after the scanned area, like strings -b 7340032
        var path = ws.Write(@"games\late.bin", bin);

        Assert.Equal("", PS1Game.GetPS1GameID(path));
    }

    [Fact]
    public void PS1_ExtractStrings_FindsPrintableRunsOfThreeOrMoreCharacters()
    {
        using var ws = new TempWorkspace();
        var data = new byte[] { 0, (byte)'a', (byte)'b', 0, (byte)'a', (byte)'b', (byte)'c', 1, 2, (byte)'B', (byte)'O', (byte)'O', (byte)'T' };
        var path = ws.Write("s.bin", data);

        Assert.Equal(new[] { "abc", "BOOT" }, PS1Game.ExtractStrings(path, 1024));
        Assert.Equal(new[] { "abc" }, PS1Game.ExtractStrings(path, 8)); // only the first bytes
    }

    [Fact]
    public void PS2_GameIdIsReadFromTheIso()
    {
        using var ws = new TempWorkspace();
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SYSTEM.CNF", new byte[64]), ("SLUS_212.58", new byte[4096]));

        // Regression: on Linux/macOS Path.GetFileName does not split "\SLUS_212.58" (ISO paths use backslashes)
        Assert.Equal("SLUS_212.58", PS2Game.GetPS2GameID(iso));
        Assert.Equal("", PS2Game.GetPS2GameID(ws.Write("notaniso.iso", "nope")));
    }

    [Theory]
    [InlineData(new[] { "SYSTEM.CNF", "SLUS_212.58;1" }, "SLUS_212.58")]
    [InlineData(new[] { "README.TXT", "SCES_500.01" }, "SCES_500.01")]
    [InlineData(new[] { "IOP", "MODULES" }, "")]
    public void PS2_FindGameIDInFileNames(string[] names, string expected)
    {
        Assert.Equal(expected, PS2Game.FindGameIDInFileNames(names));
    }

    [Fact]
    public void PS2_BootElfCrcIsTheXorOfTheElfWords()
    {
        using var ws = new TempWorkspace();
        var elf = ws.Write("elf.bin", new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0xFF, 0, 0, 0x80, 0xAA }); // trailing byte ignored

        Assert.Equal("800000FC", IsoTools.GetGameCRC(elf)); // 1 ^ 2 ^ 0x800000FF
        Assert.Equal("", IsoTools.GetGameCRC(""));
        Assert.Equal("", IsoTools.GetGameCRC(ws.PathOf("missing.elf")));
    }

    [Fact]
    public void PS2_CrcOfALargeElfReadInSeveralChunks()
    {
        using var ws = new TempWorkspace();
        var data = Noise(3 * 1048576 + 7, 11);
        uint expected = 0;
        for (int i = 0; i + 4 <= data.Length; i += 4) expected ^= BitConverter.ToUInt32(data, i);

        Assert.Equal(expected.ToString("X8"), IsoTools.GetGameCRC(ws.Write("big.elf", data)));
    }

    // The managed implementations must give exactly what the original Windows tools gave.

    [WindowsToolsFact]
    public void Equivalence_CrcMatchesTheOriginalCrcExe()
    {
        using var ws = new TempWorkspace(linkTools: true);
        foreach (var size in new[] { 4096, 8193, 1048579 })
        {
            var elf = ws.Write($"elf_{size}.bin", Noise(size, size));
            var original = ProcessRunner.Run(Path.Combine(ws.Tools, "PS4", "crc.exe"), $"\"{elf}\"").Output.Replace("crc:", "").Trim();
            Assert.Equal(original, IsoTools.GetGameCRC(elf));
        }
    }

    [WindowsToolsFact]
    public void Equivalence_PS1IdMatchesTheOriginalStringsExe()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = CreatePS1Disc(ws);
        var output = ProcessRunner.Run(Path.Combine(ws.Tools, "strings.exe"), $"/accepteula -nobanner -b 7340032 \"{bin}\"").Output;

        Assert.Equal(PS1Game.FindGameID(output.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)), PS1Game.GetPS1GameID(bin));
    }

    [WindowsToolsFact]
    public void Equivalence_PS2IdMatchesTheOriginal7zListing()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SYSTEM.CNF", new byte[64]), ("SLUS_212.58", new byte[4096]));
        var listing = ProcessRunner.Run(Path.Combine(ws.Tools, "7z.exe"), $"l -ba \"{iso}\"").Output;

        Assert.Equal(PS2Game.ParseGameIDFromListing(listing.Split(new[] { "\r\n" }, StringSplitOptions.None)), PS2Game.GetPS2GameID(iso));
    }
    [RequiresToolRunnerFact]
    public void PS1_BuildsAPackageEndToEnd()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = CreatePS1Disc(ws);
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);

        var result = PS1PackageBuilder.Build(new PS1BuildRequest
        {
            Title = "Test Game",
            NPTitle = "SLUS00594",
            DetectedGameID = "SLUS-00594",
            Disc1 = bin,
            Upscaling = "2",
        }, output);

        Assert.True(result.Success, result.Output);
        var pkg = Assert.Single(Directory.GetFiles(output, "*.pkg"));
        Assert.True(new FileInfo(pkg).Length > 1_000_000);

        // The emulator configuration and the GP4 project must describe the selected disc.
        Assert.Contains("--image=\"data/disc1.bin\"", File.ReadAllText(Path.Combine(ws.Cache, "PS1fPKG", "config-title.txt")));
        var gp4 = File.ReadAllText(Path.Combine(ws.Cache, "PS1fPKG.gp4"));
        Assert.Contains("data/disc1.bin", gp4);
        Assert.Contains("data/disc1.cue", gp4);
        Assert.True(File.Exists(Path.Combine(ws.Cache, "PS1fPKG", "data", "disc1.toc")));
    }

    [RequiresToolRunnerFact]
    public void PS1_MultiDiscPackageListsEveryDisc()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var disc1 = CreatePS1Disc(ws, "disc1");
        var disc2 = CreatePS1Disc(ws, "disc2");
        var disc3 = CreatePS1Disc(ws, "disc3");
        var disc4 = CreatePS1Disc(ws, "disc4");
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);

        var result = PS1PackageBuilder.Build(new PS1BuildRequest
        {
            Title = "Test Game", NPTitle = "SLUS00594", DetectedGameID = "SLUS-00594",
            Disc1 = disc1, Disc2 = disc2, Disc3 = disc3, Disc4 = disc4,
        }, output);

        Assert.True(result.Success, result.Output);
        var gp4 = File.ReadAllText(Path.Combine(ws.Cache, "PS1fPKG.gp4"));
        foreach (var n in new[] { 1, 2, 3, 4 })
        {
            Assert.Contains($"data/disc{n}.bin", gp4);
            Assert.Contains($"data/disc{n}.cue", gp4);
        }
    }

    [RequiresToolRunnerFact]
    public void PS1_MissingCueFileGivesAClearError()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = ws.Write(@"games\nocue.bin", new byte[2352 * 10]);
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);

        var ex = Assert.Throws<FileNotFoundException>(() =>
            PS1PackageBuilder.Build(new PS1BuildRequest { Title = "T", NPTitle = "SLUS00594", Disc1 = bin }, output));
        Assert.Contains("CUE", ex.Message);
    }

    [RequiresToolRunnerFact]
    public void PS1_TocMatchesWhatCue2tocWritesNextToTheGameAndLeavesTheGameFolderUntouched()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = CreatePS1Disc(ws);
        var cue = Path.ChangeExtension(bin, ".cue");
        var gameFolder = Path.GetDirectoryName(bin);

        // Reference: what the original code produced, running cue2toc directly in a copy of the game folder.
        var referenceFolder = Path.Combine(ws.Root, "reference");
        Directory.CreateDirectory(referenceFolder);
        File.Copy(bin, Path.Combine(referenceFolder, "game.bin"));
        File.Copy(cue, Path.Combine(referenceFolder, "game.cue"));
        var reference = ToolRunner.Run(Path.Combine(ws.Tools, "cue2toc.exe"), new[] { Platform.ToolPath(Path.Combine(referenceFolder, "game.cue")) }, referenceFolder);
        Assert.Equal(0, reference.ExitCode);
        var expected = File.ReadAllBytes(Path.Combine(referenceFolder, "game.TOC"));

        var toc = CueToc.Generate(cue, bin);

        Assert.NotEmpty(toc);
        Assert.Equal(expected, toc);
        // Nothing is written next to the user's game files.
        Assert.Equal(new[] { "game.bin", "game.cue" }, Directory.GetFiles(gameFolder).Select(Path.GetFileName).OrderBy(n => n));
    }

    [RequiresToolRunnerFact]
    public void PS1_TocGenerationFailsClearlyForMissingFiles()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = ws.Write(@"games\only.bin", new byte[2352 * 10]);

        Assert.Throws<FileNotFoundException>(() => CueToc.Generate(Path.ChangeExtension(bin, ".cue"), bin));
        var cue = ws.Write(@"games\ok.cue", "FILE \"ok.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
        Assert.Throws<FileNotFoundException>(() => CueToc.Generate(cue, Path.Combine(ws.Root, "games", "ok.bin")));
    }

    [RequiresToolRunnerFact]
    public void PS1_BuildLeavesTheGameFolderUntouchedAndCreatesATocPerDisc()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var disc1 = CreatePS1Disc(ws, "disc1");
        var disc2 = CreatePS1Disc(ws, "disc2");
        var gameFolder = Path.GetDirectoryName(disc1);
        var before = Directory.GetFiles(gameFolder).OrderBy(f => f).ToArray();
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);

        var result = PS1PackageBuilder.Build(new PS1BuildRequest
        {
            Title = "T", NPTitle = "SLUS00594", DetectedGameID = "SLUS-00594", Disc1 = disc1, Disc2 = disc2,
        }, output);

        Assert.True(result.Success, result.Output);
        Assert.Equal(before, Directory.GetFiles(gameFolder).OrderBy(f => f).ToArray());
        Assert.True(File.Exists(Path.Combine(ws.Cache, "PS1fPKG", "data", "disc1.toc")));
        Assert.True(File.Exists(Path.Combine(ws.Cache, "PS1fPKG", "data", "disc2.toc")));
    }

    [RequiresToolRunnerFact]
    public void PS2_DerivesTheGameIdFromTheNpTitleWhenItWasNotDetected()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SLUS_212.58", Noise(8192)));
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        var emulator = Directory.GetDirectories(Path.Combine(TestEnvironment.ToolsDirectory, "PS4", "emus"))
            .Select(Path.GetFileName).First(n => n != "ps1hd" && n != "psphd");

        var result = PS2PackageBuilder.Build(new PS2BuildRequest
        {
            Title = "Manual", NPTitle = "SLUS21258", DetectedGameID = "", Disc1 = iso, Emulator = emulator,
            Uprender = "2x2", Upscaling = "4X", DisplayMode = "Normal",
        }, output);

        Assert.True(result.Success, result.Output);
        Assert.Contains("--ps2-title-id=SLUS-21258", File.ReadAllText(Path.Combine(ws.Cache, "PS2fPKG", "config-emu-ps4.txt")));
        Assert.True(Directory.Exists(Path.Combine(ws.Cache, "PS2fPKG", "feature_data", "SLUS-21258")));
        Assert.Contains("0000001", Path.GetFileName(Assert.Single(Directory.GetFiles(output, "*.pkg"))));
    }

    [Fact]
    public void PS2_BuildWithoutAnyUsableIdThrowsBeforeTouchingTheCache()
    {
        using var ws = new TempWorkspace();
        Assert.Throws<ArgumentException>(() =>
            PS2PackageBuilder.Build(new PS2BuildRequest { NPTitle = "SHORT", DetectedGameID = "" }, Path.Combine(ws.Root, "out")));
        Assert.False(Directory.Exists(ws.Cache));
    }

    [RequiresToolRunnerFact]
    public void PS1_LibcryptProtectedGamesAskForConfirmation()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = CreatePS1Disc(ws);
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        int asked = 0;

        PS1PackageBuilder.Build(new PS1BuildRequest
        {
            Title = "T", NPTitle = "SLES01226", DetectedGameID = "SLES-01226", Disc1 = bin,
            ConfirmLibcrypt = () => { asked++; return true; },
        }, output);

        Assert.Equal(1, asked);
        Assert.Contains("--libcrypt=03E7", File.ReadAllText(Path.Combine(ws.Cache, "PS1fPKG", "config-title.txt")));
    }

    [RequiresToolRunnerFact]
    public void PSP_BuildsAPackageAndWarnsWhenTheEbootCannotBeDecrypted()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "psp.iso"),
            (@"PSP_GAME\PARAM.SFO", Noise(512)),
            ("UMD_DATA.BIN", System.Text.Encoding.ASCII.GetBytes("ULUS-10041|0123456789")),
            (@"PSP_GAME\SYSDIR\EBOOT.BIN", Noise(4096, 3)));
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        var warnings = new List<string>();

        var result = PSPPackageBuilder.Build(new PSPBuildRequest
        {
            Title = "Test PSP", NPTitle = "ULUS10041", IsoFile = iso, Warn = warnings.Add,
        }, output);

        Assert.True(result.Success, result.Output);
        Assert.Single(Directory.GetFiles(output, "*.pkg"));
        Assert.Contains(warnings, w => w.Contains("EBOOT.BIN"));
        Assert.False(File.Exists(Path.Combine(ws.Cache, "temp_eboot.bin")), "temp files must be removed");
        Assert.Contains("--image=\"data/USER_L0.IMG\"", File.ReadAllText(Path.Combine(ws.Cache, "PSPfPKG", "config-title.txt")));
    }

    [RequiresToolRunnerFact]
    public void PS2_BuildsAPackageEndToEnd()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SLUS_212.58", Noise(8192)));
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);
        var emulator = Directory.GetDirectories(Path.Combine(TestEnvironment.ToolsDirectory, "PS4", "emus"))
            .Select(Path.GetFileName).First(n => n != "ps1hd" && n != "psphd");

        var result = PS2PackageBuilder.Build(new PS2BuildRequest
        {
            Title = "Test PS2", NPTitle = "SLUS21258", DetectedGameID = "SLUS_212.58",
            Disc1 = iso, Emulator = emulator,
            Uprender = "2x2", Upscaling = "4X", DisplayMode = "Normal",
            AddTxtConfigFromDatabase = true, FixGraphics = true,
        }, output);

        Assert.True(result.Success, result.Output);
        Assert.Single(Directory.GetFiles(output, "*.pkg"));
        var config = File.ReadAllText(Path.Combine(ws.Cache, "PS2fPKG", "config-emu-ps4.txt"));
        Assert.Contains("--ps2-title-id=SLUS-21258", config);
        Assert.Contains("--max-disc-num=1", config);
        Assert.Contains("#Fix Graphics", config);
        Assert.Contains("image/disc01.iso", File.ReadAllText(Path.Combine(ws.Cache, "PS2fPKG.gp4")));
    }
}
