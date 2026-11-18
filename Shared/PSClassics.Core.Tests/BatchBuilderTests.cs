using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class BatchBuilderTests
{
    private static byte[] Noise(int length, int seed = 1)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    private static string Zip(TempWorkspace ws, string name, string sourceFolder)
    {
        var zip = Path.Combine(ws.Root, name);
        var result = ToolRunner.Run(Path.Combine(ws.Tools, "7z.exe"), new[] { "a", Platform.ToolPath(zip), Platform.ToolPath(Path.Combine(ws.Root, sourceFolder, "*")) });
        Assert.Equal(0, result.ExitCode);
        return zip;
    }

    private static void MakePS1Disc(TempWorkspace ws, string folder, string name = "game")
    {
        var bin = new byte[2352 * 300];
        var boot = System.Text.Encoding.ASCII.GetBytes("BOOT = cdrom:\\FOLDER\\SLUS_005.94;1\r\nTCB = 4\r\n");
        Array.Copy(boot, 0, bin, 4096, boot.Length);
        ws.Write($@"{folder}\{name}.bin", bin);
        ws.Write($@"{folder}\{name}.cue", $"FILE \"{name}.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");
    }

    private static string MakeIso(string path, params (string Path, byte[] Data)[] files)
    {
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "TEST" };
        foreach (var (name, data) in files) builder.AddFile(name, data);
        builder.Build(path);
        return path;
    }

    private static BatchOptions Options(TempWorkspace ws, List<string> log = null) => new()
    {
        OutputFolder = Path.Combine(ws.Root, "out"),
        WorkFolder = Path.Combine(ws.Root, "work"),
        CoverCacheFolder = Path.Combine(ws.Root, "covers"),
        FindCovers = false,
        FindTitles = false,
        Report = log == null ? null : item => log.Add(item.Status + ":" + item.Message),
    };

    [RequiresToolRunnerFact]
    public void BuildsAPS1GameFromAZip()
    {
        using var ws = new TempWorkspace(linkTools: true);
        MakePS1Disc(ws, "ps1");
        var item = new BatchItem { ArchiveFile = Zip(ws, "ps1.zip", "ps1") };
        Directory.CreateDirectory(Path.Combine(ws.Root, "out"));
        var options = Options(ws);

        BatchBuilder.BuildOne(item, options);

        Assert.Equal(BatchStatus.Done, item.Status);
        Assert.Equal("PS1", item.PlatformName);
        Assert.Equal("SLUS00594", item.NPTitle); // the folder of the BOOT file is not part of the ID
        Assert.True(File.Exists(item.PackagePath));
        Assert.Single(Directory.GetFiles(options.OutputFolder, "*.pkg"));
        Assert.False(Directory.Exists(Path.Combine(options.WorkFolder, "ps1")), "the extracted files are removed");
        Assert.True(File.Exists(item.ArchiveFile), "the archive is never touched");
    }

    [RequiresToolRunnerFact]
    public void BuildsAMultiTrackPS1GameFromAZip()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var first = Noise(2352 * 300);
        var boot = System.Text.Encoding.ASCII.GetBytes("BOOT = cdrom:\\SLUS_005.94;1\r\n");
        Array.Copy(boot, 0, first, 4096, boot.Length);
        ws.Write(@"multi\Game (Track 1).bin", first);
        ws.Write(@"multi\Game (Track 2).bin", Noise(2352 * 50, 2));
        ws.Write(@"multi\Game.cue",
            "FILE \"Game (Track 1).bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n" +
            "FILE \"Game (Track 2).bin\" BINARY\r\n  TRACK 02 AUDIO\r\n    INDEX 00 00:00:00\r\n    INDEX 01 00:02:00\r\n");
        var item = new BatchItem { ArchiveFile = Zip(ws, "multi.zip", "multi") };
        Directory.CreateDirectory(Path.Combine(ws.Root, "out"));

        BatchBuilder.BuildOne(item, Options(ws));

        Assert.Equal(BatchStatus.Done, item.Status);
        Assert.Equal("SLUS00594", item.NPTitle);
        Assert.NotEqual("", item.Title); // from the title list, or else the name of the archive
    }

    [RequiresToolRunnerFact]
    public void BuildsAPS2GameFromAZip()
    {
        using var ws = new TempWorkspace(linkTools: true);
        Directory.CreateDirectory(Path.Combine(ws.Root, "ps2"));
        MakeIso(Path.Combine(ws.Root, "ps2", "Game (USA).iso"), ("SLUS_212.58", Noise(8192)));
        var item = new BatchItem { ArchiveFile = Zip(ws, "ps2.zip", "ps2") };
        Directory.CreateDirectory(Path.Combine(ws.Root, "out"));

        BatchBuilder.BuildOne(item, Options(ws));

        Assert.Equal(BatchStatus.Done, item.Status);
        Assert.Equal("PS2", item.PlatformName);
        Assert.Equal("SLUS21258", item.NPTitle);
        Assert.True(File.Exists(item.PackagePath));
    }

    [RequiresToolRunnerFact]
    public void BuildsAPSPGameFromAZip()
    {
        using var ws = new TempWorkspace(linkTools: true);
        Directory.CreateDirectory(Path.Combine(ws.Root, "psp"));
        MakeIso(Path.Combine(ws.Root, "psp", "Some Game (USA).iso"),
            (@"PSP_GAME\PARAM.SFO", SfoBuilder.Build("UP9000-ULUS10041_00-ULUS10041PSPFPKG", "Some Game", "ULUS10041")),
            ("UMD_DATA.BIN", System.Text.Encoding.ASCII.GetBytes("ULUS-10041|0123456789")),
            (@"PSP_GAME\SYSDIR\EBOOT.BIN", Noise(4096, 3)));
        var item = new BatchItem { ArchiveFile = Zip(ws, "psp.zip", "psp") };
        Directory.CreateDirectory(Path.Combine(ws.Root, "out"));

        BatchBuilder.BuildOne(item, Options(ws));

        Assert.Equal(BatchStatus.Done, item.Status);
        Assert.Equal("PSP", item.PlatformName);
        Assert.Equal("Some Game", item.Title); // from the PARAM.SFO of the disc
        Assert.Equal("ULUS10041", item.NPTitle);
    }

    [RequiresToolRunnerFact]
    public void AGameThatFailsDoesNotStopTheQueue()
    {
        using var ws = new TempWorkspace(linkTools: true);
        MakePS1Disc(ws, "first");
        ws.Write(@"nogame\readme.txt", "nothing");
        MakePS1Disc(ws, "last");
        var items = new[]
        {
            new BatchItem { ArchiveFile = Zip(ws, "first.zip", "first") },
            new BatchItem { ArchiveFile = Zip(ws, "nogame.zip", "nogame") },
            new BatchItem { ArchiveFile = Zip(ws, "gone.zip", "last") },
        };
        File.Delete(items[2].ArchiveFile); // the third archive disappeared before its turn
        Directory.CreateDirectory(Path.Combine(ws.Root, "out"));
        var log = new List<string>();

        var made = BatchBuilder.BuildAll(items, Options(ws, log));

        Assert.Equal(1, made);
        Assert.Equal(BatchStatus.Done, items[0].Status);
        Assert.Equal(BatchStatus.Failed, items[1].Status);
        Assert.Contains("No game files", items[1].Message);
        Assert.Equal(BatchStatus.Failed, items[2].Status);
        Assert.Contains(log, line => line.StartsWith("Working:"));
    }

    [RequiresToolRunnerFact]
    public void CancelStopsBeforeTheNextGame()
    {
        using var ws = new TempWorkspace(linkTools: true);
        MakePS1Disc(ws, "a");
        var items = new[] { new BatchItem { ArchiveFile = Zip(ws, "a.zip", "a") } };
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        var made = BatchBuilder.BuildAll(items, Options(ws), cancel.Token);

        Assert.Equal(0, made);
        Assert.Equal(BatchStatus.Waiting, items[0].Status);
    }

    [Fact]
    public void ADoneGameIsNotBuiltAgain()
    {
        using var ws = new TempWorkspace();
        var item = new BatchItem { ArchiveFile = "x.zip", Status = BatchStatus.Done };

        Assert.Equal(0, BatchBuilder.BuildAll(new[] { item }, Options(ws)));
        Assert.Equal(BatchStatus.Done, item.Status);
    }

    [Theory]
    [InlineData("Tekken 3 (USA)", "Tekken 3")]
    [InlineData("Final Fantasy VII (USA) (Disc 1)", "Final Fantasy VII")]
    [InlineData("game", "game")]
    public void TitleFromFileName_DropsTheTags(string name, string expected)
    {
        Assert.Equal(expected, BatchBuilder.TitleFromFileName(name));
    }
}
