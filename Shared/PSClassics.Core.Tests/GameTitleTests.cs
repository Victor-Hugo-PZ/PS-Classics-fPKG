using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class GameTitleTests
{
    private const string Dat = "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/Sony%20-%20PlayStation.dat";

    [Fact]
    public void SfoReader_ReadsTheTitleOfAParamSfo()
    {
        var sfo = SfoBuilder.Build("UP9000-SLUS00402_00-SLUS00402PS1FPKG", "Tekken 3", "SLUS00402");

        Assert.Equal("Tekken 3", SfoReader.ReadTitle(sfo));
        var values = SfoReader.ReadStrings(sfo);
        Assert.Equal("SLUS00402", values["TITLE_ID"]);
        Assert.Equal("gd", values["CATEGORY"]);
    }

    [Fact]
    public void SfoReader_IgnoresDataThatIsNotAParamSfo()
    {
        Assert.Empty(SfoReader.ReadStrings(new byte[] { 1, 2, 3 }));
        Assert.Empty(SfoReader.ReadStrings(new byte[64]));
        Assert.Equal("", SfoReader.ReadTitle(null!));
    }

    [Fact]
    public void GetPSPTitle_ReadsTheTitleOfThePspIso()
    {
        using var ws = new TempWorkspace();
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "PSP" };
        builder.AddFile(@"PSP_GAME\PARAM.SFO", SfoBuilder.Build("UP9000-ULUS10041_00-ULUS10041PSPFPKG", "Lumines", "ULUS10041"));
        var iso = Path.Combine(ws.Root, "psp.iso");
        builder.Build(iso);

        Assert.Equal("Lumines", IsoTools.GetPSPTitle(iso));
    }

    [Fact]
    public void GetPSPTitle_IsEmptyForAnIsoWithoutParamSfo()
    {
        using var ws = new TempWorkspace();
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "PS2" };
        builder.AddFile("SYSTEM.CNF", new byte[16]);
        var iso = Path.Combine(ws.Root, "ps2.iso");
        builder.Build(iso);

        Assert.Equal("", IsoTools.GetPSPTitle(iso));
        Assert.Equal("", IsoTools.GetPSPTitle(Path.Combine(ws.Root, "missing.iso")));
    }

    [Theory]
    [InlineData("Tekken 3 (USA)", "Tekken 3")]
    [InlineData("Metal Gear Solid (USA) (Disc 1)", "Metal Gear Solid")]
    [InlineData("Asterix (Europe) (En,Fr,De,Nl)", "Asterix")]
    [InlineData("Crash Bandicoot - Warped (USA)", "Crash Bandicoot - Warped")]
    [InlineData("", "")]
    public void TitleFromRedumpName_DropsTheTags(string name, string expected)
    {
        Assert.Equal(expected, CoverSources.TitleFromRedumpName(name));
    }

    [Fact]
    public async Task FindTitleAsync_UsesTheRedumpNameOfTheSerial()
    {
        using var ws = new TempWorkspace();
        var dat = "game (\n\tname \"Tekken 3 (USA)\"\n\tserial \"SLUS-00402\"\n)\n";
        var web = new FakeWeb().Serve(Dat, System.Text.Encoding.UTF8.GetBytes(dat));
        using var client = new HttpClient(web);

        Assert.Equal("Tekken 3", await CoverSources.FindTitleAsync("PS1", "SLUS00402", ws.Root, client));
        Assert.Equal("", await CoverSources.FindTitleAsync("PS1", "SLUS99999", ws.Root, client));
        Assert.Equal("", await CoverSources.FindTitleAsync("PS1", "not an id", ws.Root, client));
        Assert.Equal("", await CoverSources.FindTitleAsync("PSP", "SLUS00402", ws.Root, client));
    }

    [Fact]
    public async Task FindTitleAsync_IsEmptyWhenTheDatabaseCannotBeRead()
    {
        using var ws = new TempWorkspace();
        using var client = new HttpClient(new FakeWeb()); // every address answers 404

        Assert.Equal("", await CoverSources.FindTitleAsync("PS1", "SLUS00402", ws.Root, client));
    }
}
