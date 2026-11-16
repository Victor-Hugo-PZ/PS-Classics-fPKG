using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class PS1GameTests
{
    [Theory]
    [InlineData(@"BOOT = cdrom:\SLUS_005.94;1", "SLUS-00594")]
    [InlineData(@"BOOT=cdrom:\SCES_012.34;1", "SCES-01234")]
    [InlineData(@"BOOT = cdrom:SLPS_023.45;1", "SLPS-02345")]
    [InlineData(@"BOOT = cdrom:\MGS\SLUS_005.94;1", "SLUS-00594")]
    public void FindGameID_ParsesTheBootEntry(string line, string expected)
    {
        Assert.Equal(expected, PS1Game.FindGameID(new[] { line }));
    }

    [Fact]
    public void FindGameID_UsesTheFirstBootEntryAndIgnoresOtherLines()
    {
        var lines = new[] { "SYSTEM.CNF", "TCB = 4", @"BOOT = cdrom:\SLUS_005.94;1", @"BOOT = cdrom:\SLUS_999.99;1" };
        Assert.Equal("SLUS-00594", PS1Game.FindGameID(lines));
    }

    [Fact]
    public void FindGameID_ReturnsEmptyWithoutBootEntry()
    {
        Assert.Equal("", PS1Game.FindGameID(new[] { "hello", "world" }));
        Assert.Equal("", PS1Game.FindGameID(Array.Empty<string>()));
    }

    [Fact]
    public void GetTitle_FindsTheTitleInTheDatabase()
    {
        using var ws = new TempWorkspace();
        var db = ws.Write("ps1ids.txt", "SLUS-00001;First\r\nSLUS-00594;Metal Gear Solid [Disc1of2]\r\nSLUS-00776;Third\r\n");
        Assert.Equal("Metal Gear Solid [Disc1of2]", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-00594", db));
    }

    [Fact]
    public void GetTitle_ReturnsEmptyForUnknownEmptyAndMalformedEntries()
    {
        using var ws = new TempWorkspace();
        var db = ws.Write("ps1ids.txt", "SLUS-00001;First\r\nSLUS-12345 line without separator\r\n");
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-99999", db));
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("", db));
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-12345", db)); // used to throw IndexOutOfRange
    }

    [Fact]
    public void GetTitle_DefaultsToTheDatabaseInTheToolsFolder()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"Tools\ps1ids.txt", "SLUS-00594;Metal Gear Solid\r\n");
        Assert.Equal("Metal Gear Solid", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-00594"));
    }

    [Fact]
    public void IsGameProtected_ReturnsTheLibcryptValue()
    {
        using var ws = new TempWorkspace();
        var db = ws.Write("libcrypt.txt", "SLES_012.26 03E7\r\nSCES_015.64 CEC1\r\n");
        Assert.Equal("03E7", PS1Game.IsGameProtected("SLES_012.26", db));
        Assert.Equal("CEC1", PS1Game.IsGameProtected("SCES_015.64", db));
        Assert.Equal("", PS1Game.IsGameProtected("SLUS_005.94", db));
        Assert.Equal("", PS1Game.IsGameProtected("", db));
    }

    [Theory]
    [InlineData("SLES-01226", "SLES01226", "SLES_012.26")]
    [InlineData("", "SLES01226", "SLES_012.26")]
    [InlineData("SLUS-00594", "SLUS00594", "SLUS_005.94")]
    [InlineData("", "SHORT", "SHORT")]
    [InlineData("SLUS", "SLUS00594", "SLUS")]
    public void FormatLibcryptID_BuildsTheLibcryptListFormat(string detected, string npTitle, string expected)
    {
        Assert.Equal(expected, PS1Game.FormatLibcryptID(detected, npTitle));
    }

    [RequiresToolsFact]
    public void RealDatabases_ContainKnownGames()
    {
        using var ws = new TempWorkspace(linkTools: true);
        Assert.Equal("Metal Gear Solid [Disc1of2]", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-00594"));
        Assert.Equal("03E7", PS1Game.IsGameProtected("SLES_012.26"));
    }
}

public class PS2GameTests
{
    [Fact]
    public void ParseGameIDFromListing_ReadsTheFileNameColumn()
    {
        var lines = new[]
        {
            "2005-05-12 10:00:00 .....         1024         1024  SYSTEM.CNF",
            "2005-05-12 10:00:00 .....      3145728      3145728  SLUS_212.58",
            "2005-05-12 10:00:00 .....         2048         2048  IOP",
        };
        Assert.Equal("SLUS_212.58", PS2Game.ParseGameIDFromListing(lines));
    }

    [Theory]
    [InlineData("Volume: SLES_500.01")]    // used to throw IndexOutOfRange: the split dropped the empty first part
    [InlineData("  Volume: SLES_500.01")]
    [InlineData("Volume: SLES_500.01  ")]
    public void ParseGameIDFromListing_ReadsTheVolumeHeader(string line)
    {
        Assert.Equal("SLES_500.01", PS2Game.ParseGameIDFromListing(new[] { line }));
    }

    [Theory]
    [InlineData("SLUS_212.58")]            // not enough columns: used to throw IndexOutOfRange
    [InlineData("a b SLUS_212.58")]
    [InlineData("Volume:SLUS_212.58")]     // no space after the colon
    public void ParseGameIDFromListing_DoesNotThrowOnMalformedLines(string line)
    {
        var ex = Record.Exception(() => PS2Game.ParseGameIDFromListing(new[] { line }));
        Assert.Null(ex);
    }

    [Fact]
    public void ParseGameIDFromListing_ReturnsEmptyWhenThereIsNoId()
    {
        Assert.Equal("", PS2Game.ParseGameIDFromListing(new[] { "2005-05-12 10:00:00 ..... 1 1 README.TXT" }));
        Assert.Equal("", PS2Game.ParseGameIDFromListing(Array.Empty<string>()));
    }

    [Theory]
    [InlineData("SLUS21258", "SLUS_212.58")]
    [InlineData("SCES50001", "SCES_500.01")]
    [InlineData("", "")]
    [InlineData("SLUS2125", "")]
    [InlineData("SLUS212588", "")]
    [InlineData(null, "")]
    public void FormatGameIDFromNPTitle_RestoresTheIsoStyleId(string npTitle, string expected)
    {
        Assert.Equal(expected, PS2Game.FormatGameIDFromNPTitle(npTitle));
    }

    [Fact]
    public void GetTitle_FindsTheTitleAndIgnoresTheDash()
    {
        using var ws = new TempWorkspace();
        var db = ws.Write("ps2ids.txt", "SLES54604;!Qu? pasa Neng!\r\nSLUS21258;.hack//G.U. Vol.1 - Rebirth\r\n");
        Assert.Equal(".hack//G.U. Vol.1 - Rebirth", PS2Game.GetPS2GameTitleFromDatabaseList("SLUS-21258", db));
    }

    [Fact]
    public void GetTitle_ReturnsPlaceholderForUnknownOrEmptyIds()
    {
        using var ws = new TempWorkspace();
        var db = ws.Write("ps2ids.txt", "SLUS21258;Some Game\r\n");
        Assert.Equal("Unknown PS2 game", PS2Game.GetPS2GameTitleFromDatabaseList("SLUS-00000", db));
        Assert.Equal("Unknown PS2 game", PS2Game.GetPS2GameTitleFromDatabaseList("", db));
    }

    [RequiresToolsFact]
    public void RealDatabase_ContainsKnownGames()
    {
        using var ws = new TempWorkspace(linkTools: true);
        Assert.Equal(".hack//G.U. Vol.1 - Rebirth", PS2Game.GetPS2GameTitleFromDatabaseList("SLUS-21258"));
    }
}
