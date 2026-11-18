using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class DiscImagesTests
{
    private const int Sector = 2352;

    /// <summary>A disc with one BIN per track, like Tekken 3: the cue sheet lists all of them, its name is not the BIN name.</summary>
    private static (string Cue, string Track1, string Track2, string Track3) CreateMultiTrackDisc(TempWorkspace ws)
    {
        var track1 = ws.Write(@"games\Game (Track 1).bin", new byte[Sector * 3]);
        var track2 = ws.Write(@"games\Game (Track 2).bin", new byte[Sector * 2]);
        var track3 = ws.Write(@"games\Game (Track 3).bin", new byte[Sector * 1]);
        var cue = ws.Write(@"games\Game.cue",
            "FILE \"Game (Track 1).bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n" +
            "FILE \"Game (Track 2).bin\" BINARY\r\n  TRACK 02 AUDIO\r\n    INDEX 00 00:00:00\r\n    INDEX 01 00:02:00\r\n" +
            "FILE \"Game (Track 3).bin\" BINARY\r\n  TRACK 03 AUDIO\r\n    INDEX 00 00:00:00\r\n    INDEX 01 00:02:00\r\n");
        return (cue, track1, track2, track3);
    }

    [Fact]
    public void FindCueFile_UsesTheCueWithTheSameName()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write(@"games\same.bin", new byte[Sector]);
        var cue = ws.Write(@"games\same.cue", "FILE \"same.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");

        Assert.Equal(cue, DiscImages.FindCueFile(bin));
    }

    [Fact]
    public void FindCueFile_FindsTheSharedCueThatListsTheBin()
    {
        // Regression: Tekken 3 has one CUE sheet ("Tekken 3 (USA).cue") for its three BIN files, not one CUE per BIN file.
        using var ws = new TempWorkspace();
        var (cue, track1, _, _) = CreateMultiTrackDisc(ws);

        Assert.Equal(cue, DiscImages.FindCueFile(track1));
    }

    [Fact]
    public void FindCueFile_ThrowsWhenNoCueListsTheBin()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write(@"games\alone.bin", new byte[Sector]);

        Assert.Throws<FileNotFoundException>(() => DiscImages.FindCueFile(bin));
    }

    [Fact]
    public void ReferencedFiles_ReadsQuotedAndUnquotedNames()
    {
        using var ws = new TempWorkspace();
        var cue = ws.Write(@"games\mixed.cue", "FILE \"A (Track 1).bin\" BINARY\r\nFILE b.bin BINARY\r\n");

        Assert.Equal(new[] { "A (Track 1).bin", "b.bin" }, DiscImages.ReferencedFiles(cue));
    }

    [Fact]
    public void MergeTracks_JoinsTheBinFilesAndMovesTheIndexesOfEachTrack()
    {
        using var ws = new TempWorkspace();
        var (cue, track1, track2, track3) = CreateMultiTrackDisc(ws);
        var outBin = Path.Combine(ws.Root, "merged.bin");
        var outCue = Path.Combine(ws.Root, "merged.cue");

        DiscImages.MergeTracks(cue, outBin, outCue);

        // The merged BIN is the three files one after the other
        Assert.Equal(Sector * (3 + 2 + 1), new FileInfo(outBin).Length);
        // Track 2 starts after the 3 sectors of track 1 (00:00:03) and its pregap is 150 sectors (2 seconds)
        var lines = File.ReadAllLines(outCue);
        Assert.Equal("FILE \"Game (Track 1).bin\" BINARY", lines[0]);
        Assert.Contains("    INDEX 00 00:00:03", lines);
        Assert.Contains("    INDEX 01 00:02:03", lines);
        // Track 3 starts after the 5 sectors of the first two files, and its pregap follows the same rule
        Assert.Contains("    INDEX 00 00:00:05", lines);
        Assert.Contains("    INDEX 01 00:02:05", lines);
        Assert.Single(lines.Where(line => line.StartsWith("FILE ")));
    }

    [Fact]
    public void MergeTracks_KeepsTheBytesOfEveryTrackInOrder()
    {
        using var ws = new TempWorkspace();
        var (cue, track1, track2, track3) = CreateMultiTrackDisc(ws);
        var outBin = Path.Combine(ws.Root, "merged.bin");

        DiscImages.MergeTracks(cue, outBin, Path.Combine(ws.Root, "merged.cue"));

        var expected = File.ReadAllBytes(track1).Concat(File.ReadAllBytes(track2)).Concat(File.ReadAllBytes(track3)).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(outBin));
    }

    [Fact]
    public void Prepare_LeavesASingleBinDiscAlone()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write(@"games\single.bin", new byte[Sector]);
        var cue = ws.Write(@"games\single.cue", "FILE \"single.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n");

        var disc = DiscImages.Prepare(1, bin, ws.Root, "work");

        Assert.False(disc.IsMerged);
        Assert.Equal(cue, disc.CueFile);
        Assert.Equal(bin, disc.BinFile);
    }

    [Fact]
    public void Prepare_MergesAMultiTrackDiscIntoTheWorkFolder()
    {
        using var ws = new TempWorkspace();
        var (_, track1, _, _) = CreateMultiTrackDisc(ws);

        var disc = DiscImages.Prepare(1, track1, ws.Root, "work-disc1");

        Assert.True(disc.IsMerged);
        Assert.Equal(Path.Combine(ws.Root, "work-disc1.bin"), disc.BinFile);
        Assert.True(File.Exists(disc.CueFile));
        disc.DeleteMergedFiles();
        Assert.False(File.Exists(disc.BinFile));
        Assert.False(File.Exists(disc.CueFile));
    }
}
