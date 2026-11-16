using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>
/// Integration tests that run the real third-party tools (7z, strings, crc, cue2toc, gengp4, orbis-pub-cmd).
/// They are skipped when the git-ignored Tools folder is not available.
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

    [RequiresToolsFact]
    public void PS1_GameIdIsReadFromTheBinWithStrings()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = CreatePS1Disc(ws);

        Assert.Equal("SLUS-00594", PS1Game.GetPS1GameID(bin));
    }

    [RequiresToolsFact]
    public void PS1_GameIdIsEmptyWhenTheBinHasNoBootEntry()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = ws.Write(@"games\empty.bin", new byte[2352 * 10]);

        Assert.Equal("", PS1Game.GetPS1GameID(bin));
    }

    [RequiresToolsFact]
    public void PS2_GameIdIsReadFromTheIsoWith7z()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SLUS_212.58", new byte[4096]), ("SYSTEM.CNF", new byte[64]));

        Assert.Equal("SLUS_212.58", PS2Game.GetPS2GameID(iso));
    }

    [RequiresToolsFact]
    public void PS2_BootElfCrcIsComputedWithCrcExe()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var iso = CreateIso(Path.Combine(ws.Root, "game.iso"), ("SLUS_212.58", Noise(8192)));
        var elf = IsoTools.GetELFfromISO(iso, "SLUS_212.58");

        var crc = IsoTools.GetGameCRC(elf);

        Assert.Matches("^[0-9A-Fa-f]{8}$", crc);
        Assert.Equal(crc, IsoTools.GetGameCRC(elf)); // deterministic
        Assert.Equal("", IsoTools.GetGameCRC(""));
    }

    [RequiresToolsFact]
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

    [RequiresToolsFact]
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

    [RequiresToolsFact]
    public void PS1_MissingCueFileGivesAClearError()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var bin = ws.Write(@"games\nocue.bin", new byte[2352 * 10]);
        var output = Path.Combine(ws.Root, "out");
        Directory.CreateDirectory(output);

        var ex = Assert.Throws<FileNotFoundException>(() =>
            PS1PackageBuilder.Build(new PS1BuildRequest { Title = "T", NPTitle = "SLUS00594", Disc1 = bin }, output));
        Assert.Contains("TOC", ex.Message);
    }

    [RequiresToolsFact]
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

    [RequiresToolsFact]
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

    [RequiresToolsFact]
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
