using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class ArchiveImportTests
{
    [Theory]
    [InlineData("game.zip", true)]
    [InlineData("GAME.ZIP", true)]
    [InlineData("game.7z", true)]
    [InlineData("game.rar", false)]
    [InlineData("game.iso", false)]
    public void IsArchive_OnlyZipAnd7z(string name, bool expected)
    {
        Assert.Equal(expected, ArchiveImport.IsArchive(name));
    }

    [Fact]
    public void FindGameFiles_GivesOneDiscPerCueSheetAndSkipsItsTrackFiles()
    {
        // Regression: a multi-track disc (Tekken 3) is one disc: its audio track BIN files are not discs of their own
        using var ws = new TempWorkspace();
        ws.Write(@"Tekken 3\Tekken 3 (USA) (Track 1).bin", new byte[2352 * 3]);
        ws.Write(@"Tekken 3\Tekken 3 (USA) (Track 2).bin", new byte[2352 * 2]);
        ws.Write(@"Tekken 3\Tekken 3 (USA) (Track 3).bin", new byte[2352 * 2]);
        ws.Write(@"Tekken 3\Tekken 3 (USA).cue",
            "FILE \"Tekken 3 (USA) (Track 1).bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n" +
            "FILE \"Tekken 3 (USA) (Track 2).bin\" BINARY\r\n  TRACK 02 AUDIO\r\n    INDEX 01 00:00:00\r\n" +
            "FILE \"Tekken 3 (USA) (Track 3).bin\" BINARY\r\n  TRACK 03 AUDIO\r\n    INDEX 01 00:00:00\r\n");

        var found = ArchiveImport.FindGameFiles(ws.Root);

        var disc = Assert.Single(found);
        Assert.Equal(DroppedFileKind.PS1Disc, disc.Kind);
        Assert.EndsWith("Tekken 3 (USA) (Track 1).bin", disc.Path);
    }

    [Fact]
    public void FindGameFiles_FindsAPS2IsoInASubfolder()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"Game\disc\Game.iso", new byte[2048 * 16]); // not a real ISO: it is not a PSP one, so it is a PS2 disc

        var found = ArchiveImport.FindGameFiles(ws.Root);

        var disc = Assert.Single(found);
        Assert.Equal(DroppedFileKind.PS2Disc, disc.Kind);
    }

    [Fact]
    public void FindGameFiles_IgnoresTheTextFilesOfTheArchive()
    {
        // Regression: "Vimm's Lair.txt" (a note of the download) was taken as the TXT config of the game
        using var ws = new TempWorkspace();
        ws.Write(@"Vimm's Lair.txt", "Downloaded from a website");
        ws.Write(@"game.lua", "-- lua");

        Assert.Empty(ArchiveImport.FindGameFiles(ws.Root));
    }

    [Fact]
    public void FindGameFiles_IsEmptyWhenTheArchiveHasNoGameFiles()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"readme.md", "nothing here");

        Assert.Empty(ArchiveImport.FindGameFiles(ws.Root));
    }
}
