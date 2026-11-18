using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class ArchiveInspectorTests
{
    private const string SevenZipListing =
        "\r\n7-Zip 22.01 (x64) : Copyright (c) 1999-2022 Igor Pavlov : 2022-07-15\r\n\r\nListing archive: Game.7z\r\n\r\n--\r\nPath = Game.7z\r\nType = 7z\r\n\r\n----------\r\n" +
        "Path = Game\\\r\nSize = 0\r\nAttributes = D\r\nEncrypted = -\r\n\r\n" +
        "Path = Game\\Game (Track 1).bin\r\nSize = 2352\r\nAttributes = AC\r\nEncrypted = -\r\n\r\n" +
        "Path = Game\\Game.cue\r\nSize = 300\r\nAttributes = AC\r\nEncrypted = -\r\n\r\n";

    private static string Cue(params string[] bins) =>
        string.Concat(bins.Select((name, i) => $"FILE \"{name}\" BINARY\r\n  TRACK {i + 1:00} {(i == 0 ? "MODE2/2352" : "AUDIO")}\r\n    INDEX 01 00:00:00\r\n"));

    private static List<ArchiveEntry> Entries(params string[] paths) =>
        paths.Select(p => new ArchiveEntry { Path = p, Size = 1000 }).ToList();

    [Fact]
    public void ParseListing_ReadsTheEntriesAfterTheHeader()
    {
        var entries = ArchiveInspector.ParseListing(SevenZipListing);

        Assert.Equal(3, entries.Count);
        Assert.True(entries[0].IsFolder); // "Attributes = D" (7z does not print "Folder")
        Assert.Equal("Game/Game (Track 1).bin", entries[1].Path);
        Assert.Equal(2352, entries[1].Size);
        Assert.False(entries[1].IsFolder);
        Assert.DoesNotContain(entries, e => e.Path == "Game.7z"); // the header block is the archive itself
    }

    [Fact]
    public void ParseListing_ReadsFolderAndEncryptedFlagsOfAZipListing()
    {
        var entries = ArchiveInspector.ParseListing("Path = a.zip\r\nType = zip\r\n\r\n----------\r\nPath = dir\r\nFolder = +\r\nSize = 0\r\n\r\nPath = dir/f.bin\r\nFolder = -\r\nSize = 5\r\nEncrypted = +\r\n");

        Assert.True(entries[0].IsFolder);
        Assert.True(entries[1].IsEncrypted);
        Assert.Equal(5, entries[1].Size);
    }

    [Fact]
    public void ParseListing_IsEmptyForText7zDoesNotPrint()
    {
        Assert.Empty(ArchiveInspector.ParseListing(""));
        Assert.Empty(ArchiveInspector.ParseListing("Can not open file as archive"));
    }

    [Fact]
    public void Analyze_AcceptsAnArchiveWithTheCueAndAllItsTracks()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"Game\Game.cue", Cue("Game (Track 1).bin", "Game (Track 2).bin"));

        var report = ArchiveInspector.Analyze(Entries("Game/Game (Track 1).bin", "Game/Game (Track 2).bin", "Game/Game.cue"), ws.Root, -1);

        Assert.True(report.IsOk, report.Describe());
    }

    [Fact]
    public void Analyze_FindsATrackThatIsNotInTheArchive()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"Game\Game.cue", Cue("Game (Track 1).bin", "Game (Track 2).bin"));

        var report = ArchiveInspector.Analyze(Entries("Game/Game (Track 1).bin", "Game/Game.cue"), ws.Root, -1);

        var issue = Assert.Single(report.Issues);
        Assert.Equal(ArchiveIssueKind.MissingTrack, issue.Kind);
        Assert.Contains("Game (Track 2).bin", issue.Message);
    }

    [Fact]
    public void Analyze_FindsAnArchiveWithoutGameFiles()
    {
        var report = ArchiveInspector.Analyze(Entries("readme.txt", "cover.jpg"), "", -1);

        Assert.Equal(ArchiveIssueKind.NoGameFiles, Assert.Single(report.Issues).Kind);
    }

    [Fact]
    public void Analyze_FindsAPasswordProtectedArchive()
    {
        var entries = Entries("Game.iso");
        entries[0].IsEncrypted = true;

        Assert.Equal(ArchiveIssueKind.Encrypted, Assert.Single(ArchiveInspector.Analyze(entries, "", -1).Issues).Kind);
    }

    [Fact]
    public void Analyze_FindsNotEnoughFreeSpace()
    {
        var report = ArchiveInspector.Analyze(Entries("Game.iso"), "", 500);

        Assert.Equal(ArchiveIssueKind.NotEnoughSpace, Assert.Single(report.Issues).Kind);
        Assert.True(ArchiveInspector.Analyze(Entries("Game.iso"), "", 5000).IsOk);
        Assert.True(ArchiveInspector.Analyze(Entries("Game.iso"), "", -1).IsOk);
    }

    private static string MakeZip(TempWorkspace ws, string zipName, string folder, params string[] extraArgs)
    {
        var zip = Path.Combine(ws.Root, zipName);
        var args = new List<string> { "a", Platform.ToolPath(zip), Platform.ToolPath(Path.Combine(ws.Root, folder, "*")) };
        args.AddRange(extraArgs);
        var result = ToolRunner.Run(Path.Combine(ws.Tools, "7z.exe"), args);
        Assert.Equal(0, result.ExitCode);
        return zip;
    }

    [RequiresToolRunnerFact]
    public void Inspect_RealArchives_CompleteIncompleteAndEmpty()
    {
        using var ws = new TempWorkspace(linkTools: true);
        ws.Write(@"full\Game (Track 1).bin", new byte[2352 * 3]);
        ws.Write(@"full\Game (Track 2).bin", new byte[2352 * 2]);
        ws.Write(@"full\Game.cue", Cue("Game (Track 1).bin", "Game (Track 2).bin"));
        ws.Write(@"partial\Game (Track 1).bin", new byte[2352 * 3]);
        ws.Write(@"partial\Game.cue", Cue("Game (Track 1).bin", "Game (Track 2).bin"));
        ws.Write(@"empty\readme.txt", "nothing");

        Assert.True(ArchiveInspector.Inspect(MakeZip(ws, "full.zip", "full")).IsOk);
        Assert.Equal(ArchiveIssueKind.MissingTrack, Assert.Single(ArchiveInspector.Inspect(MakeZip(ws, "partial.zip", "partial")).Issues).Kind);
        Assert.Equal(ArchiveIssueKind.NoGameFiles, Assert.Single(ArchiveInspector.Inspect(MakeZip(ws, "empty.zip", "empty")).Issues).Kind);
    }

    [RequiresToolRunnerFact]
    public void Inspect_RealPasswordProtectedZip()
    {
        using var ws = new TempWorkspace(linkTools: true);
        ws.Write(@"secret\Game.iso", new byte[4096]);

        var report = ArchiveInspector.Inspect(MakeZip(ws, "secret.zip", "secret", "-pSECRET"));

        Assert.Equal(ArchiveIssueKind.Encrypted, Assert.Single(report.Issues).Kind);
    }

    [RequiresToolRunnerFact]
    public void Inspect_ThrowsForAFileThatIsNotAnArchive()
    {
        using var ws = new TempWorkspace(linkTools: true);
        var fake = ws.Write("fake.zip", "this is not a zip");

        Assert.Throws<InvalidOperationException>(() => ArchiveInspector.Inspect(fake));
    }
}
