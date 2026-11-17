using Xunit;

namespace makePSClassicsfPKG.Tests;

/// <summary>Mirrors the Windows tests of PS1Game/PS2Game: both apps share the same game ID parsing logic.</summary>
public class PS1GameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "psfpkg-linux-tests", Guid.NewGuid().ToString("N"));

    public PS1GameTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void GetTitle_FindsTheTitleInTheDatabase()
    {
        var db = Write("ps1ids.txt", "SLUS-00001;First\nSLUS-00594;Metal Gear Solid [Disc1of2]\n");
        Assert.Equal("Metal Gear Solid [Disc1of2]", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-00594", db));
    }

    [Fact]
    public void GetTitle_ReturnsEmptyForUnknownEmptyAndMalformedEntries()
    {
        var db = Write("ps1ids.txt", "SLUS-00001;First\nSLUS-12345 line without separator\n");
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-99999", db));
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("", db));
        Assert.Equal("", PS1Game.GetPS1GameTitleFromDatabaseList("SLUS-12345", db)); // used to throw IndexOutOfRange
    }

    [Fact]
    public void IsGameProtected_ReturnsTheLibcryptValue()
    {
        var db = Write("libcrypt.txt", "SLES_012.26 03E7\nSCES_015.64 CEC1\n");
        Assert.Equal("03E7", PS1Game.IsGameProtected("SLES_012.26", db));
        Assert.Equal("", PS1Game.IsGameProtected("SLUS_005.94", db));
        Assert.Equal("", PS1Game.IsGameProtected("", db));
    }
}

public class PS2GameTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "psfpkg-linux-tests", Guid.NewGuid().ToString("N"));

    public PS2GameTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    [Fact]
    public void ParseGameIDFromListing_ReadsTheFileNameColumn()
    {
        var lines = new[]
        {
            "2005-05-12 10:00:00 .....         1024         1024  SYSTEM.CNF",
            "2005-05-12 10:00:00 .....      3145728      3145728  SLUS_212.58",
        };
        Assert.Equal("SLUS_212.58", PS2Game.ParseGameIDFromListing(lines));
    }

    [Theory]
    [InlineData("Volume: SLES_500.01")]
    [InlineData("  Volume: SLES_500.01")]
    public void ParseGameIDFromListing_ReadsTheVolumeHeader(string line)
    {
        Assert.Equal("SLES_500.01", PS2Game.ParseGameIDFromListing(new[] { line }));
    }

    [Theory]
    [InlineData("SLUS_212.58")]
    [InlineData("a b SLUS_212.58")]
    [InlineData("Volume:SLUS_212.58")]
    public void ParseGameIDFromListing_DoesNotThrowOnMalformedLines(string line)
    {
        Assert.Null(Record.Exception(() => PS2Game.ParseGameIDFromListing(new[] { line })));
    }

    [Fact]
    public void GetTitle_FindsTheTitleAndReturnsPlaceholderForUnknownIds()
    {
        var db = Path.Combine(_dir, "ps2ids.txt");
        File.WriteAllText(db, "SLUS21258;.hack//G.U. Vol.1 - Rebirth\n");

        Assert.Equal(".hack//G.U. Vol.1 - Rebirth", PS2Game.GetPS2GameTitleFromDatabaseList("SLUS-21258", db));
        Assert.Equal("Unknown PS2 game", PS2Game.GetPS2GameTitleFromDatabaseList("SLUS-00000", db));
        Assert.Equal("Unknown PS2 game", PS2Game.GetPS2GameTitleFromDatabaseList("", db));
    }
}
