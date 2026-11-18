using System.Net;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>Answers requests from a fixed table of URLs, so the cover search runs without the network.</summary>
internal sealed class FakeWeb : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files = new();
    public List<string> Requests { get; } = new();

    public FakeWeb Serve(string url, byte[] content)
    {
        _files[url] = content;
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.AbsoluteUri; // the escaped form the code sent (ToString() would unescape it)
        Requests.Add(url);
        if (_files.TryGetValue(url, out var content))
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}

public class CoverSourcesTests
{
    private const string BoxArt = "https://raw.githubusercontent.com/libretro-thumbnails/Sony_-_PlayStation/master/Named_Boxarts/";
    private const string Dat = "https://raw.githubusercontent.com/libretro/libretro-database/master/metadat/redump/Sony%20-%20PlayStation.dat";

    private const string SampleDat =
        "clrmamepro (\n\tname \"Sony - PlayStation\"\n)\n" +
        "game (\n\tname \"Metal Gear Solid (USA) (Disc 1)\"\n\tregion \"USA\"\n\tserial \"SLUS-00594\"\n" +
        "\trom ( name \"Metal Gear Solid (USA) (Disc 1).bin\" serial \"SLUS-00594\" )\n)\n" +
        "game (\n\tname \"Disney's Atlantis - The Lost Empire (USA, Canada)\"\n\tserial \"SCUS-94636\"\n)\n";

    private static byte[] Png(string dir)
    {
        var path = TestImages.Save(Path.Combine(dir, "box.png"), 60, 80);
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void ParseSerialNames_ReadsTheGameNameOfEachSerial()
    {
        var names = CoverSources.ParseSerialNames(SampleDat);
        Assert.Equal("Metal Gear Solid (USA) (Disc 1)", names["SLUS-00594"]);
        Assert.Equal("Disney's Atlantis - The Lost Empire (USA, Canada)", names["SCUS-94636"]);
        Assert.False(names.ContainsKey("Sony - PlayStation")); // the header is not a game
    }

    [Fact]
    public void ParseSerialNames_KeepsTheFirstNameOfASerial()
    {
        var names = CoverSources.ParseSerialNames("game (\n\tname \"First (USA)\"\n\tserial \"SLUS-1\"\n)\ngame (\n\tname \"Second (USA)\"\n\tserial \"SLUS-1\"\n)\n");
        Assert.Equal("First (USA)", names["SLUS-1"]);
    }

    [Fact]
    public void LibretroNameCandidates_DropsDiscPartAndExtraTagsStepByStep()
    {
        var names = CoverSources.LibretroNameCandidates("Metal Gear Solid (USA) (Disc 1)");
        Assert.Equal("Metal Gear Solid (USA)", names[0]);

        var rerelease = CoverSources.LibretroNameCandidates("Armored Core (USA) (Rerelease)");
        Assert.Equal(new[] { "Armored Core (USA) (Rerelease)", "Armored Core (USA)" }, rerelease.Take(2));

        var ps2 = CoverSources.LibretroNameCandidates("Grand Theft Auto - San Andreas (USA) (v1.03)");
        Assert.Contains("Grand Theft Auto - San Andreas (USA)", ps2);
    }

    [Fact]
    public void LibretroNameCandidates_UsesOnlyTheFirstRegionOfAGroup()
    {
        var names = CoverSources.LibretroNameCandidates("Disney's Atlantis - The Lost Empire (USA, Canada)");
        Assert.Contains("Disney's Atlantis - The Lost Empire (USA)", names);
        Assert.Equal("Disney's Atlantis - The Lost Empire (USA, Canada)", names[0]);
    }

    [Fact]
    public void LibretroNameCandidates_KeepsLanguageLists()
    {
        var names = CoverSources.LibretroNameCandidates("Asterix (Europe) (En,Fr,De,Nl)");
        Assert.Equal("Asterix (Europe) (En,Fr,De,Nl)", names[0]);
    }

    [Fact]
    public void TitleNameCandidates_TriesTheCommonRegionsAndReplacesColons()
    {
        var names = CoverSources.TitleNameCandidates("Jak and Daxter: The Precursor Legacy");
        Assert.Equal("Jak and Daxter - The Precursor Legacy (USA)", names[0]);
        Assert.Contains("Jak and Daxter - The Precursor Legacy (Europe)", names);
        Assert.Empty(CoverSources.TitleNameCandidates("   "));
    }

    [Fact]
    public async Task FindCover_UsesTheIdCollectionFirst()
    {
        using var ws = new TempWorkspace();
        var web = new FakeWeb().Serve("https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/SLUS-00594.jpg", new byte[] { 1, 2, 3 });
        using var client = new HttpClient(web);

        var found = await CoverSources.FindCoverAsync("PS1", "SLUS-00594", "Metal Gear Solid", ws.Root, client);

        Assert.NotNull(found);
        Assert.Equal(CoverSource.IdCollection, found.Source);
        Assert.Equal(new byte[] { 1, 2, 3 }, found.Image);
        Assert.Empty(web.Requests.Where(url => url.Contains("Named_Boxarts")));
    }

    [Fact]
    public async Task FindCover_FallsBackToTheNameOfTheRedumpDatabase()
    {
        using var ws = new TempWorkspace();
        var png = Png(ws.Root);
        // The ID collection does not have this game; the Redump name of the serial has a box art
        var web = new FakeWeb()
            .Serve(Dat, System.Text.Encoding.UTF8.GetBytes(SampleDat))
            .Serve(BoxArt + "Metal%20Gear%20Solid%20%28USA%29.png", png);
        using var client = new HttpClient(web);

        var found = await CoverSources.FindCoverAsync("PS1", "SLUS-00594", "", ws.Root, client);

        Assert.NotNull(found);
        Assert.Equal(CoverSource.NameDatabase, found.Source);
        Assert.Equal(png, found.Image);
        // The Redump names are cached in the covers folder
        Assert.True(File.Exists(Path.Combine(ws.Root, "redump-PS1.dat")));
    }

    [Fact]
    public async Task FindCover_UsesTheTypedTitleWhenTheIdIsNotInTheDatabase()
    {
        using var ws = new TempWorkspace();
        var png = Png(ws.Root);
        var web = new FakeWeb()
            .Serve(Dat, System.Text.Encoding.UTF8.GetBytes(SampleDat))
            .Serve(BoxArt + "Crash%20Bandicoot%20%28USA%29.png", png);
        using var client = new HttpClient(web);

        var found = await CoverSources.FindCoverAsync("PS1", "SLUS-99999", "Crash Bandicoot", ws.Root, client);

        Assert.NotNull(found);
        Assert.Equal(CoverSource.TitleSearch, found.Source);
    }

    [Fact]
    public async Task FindCover_ReturnsNothingWhenNoSourceHasTheGame()
    {
        using var ws = new TempWorkspace();
        var web = new FakeWeb().Serve(Dat, System.Text.Encoding.UTF8.GetBytes(SampleDat));
        using var client = new HttpClient(web);

        Assert.Null(await CoverSources.FindCoverAsync("PS1", "SLUS-99999", "Unknown Game", ws.Root, client));
    }

    [Fact]
    public async Task FindCover_UsesTheCachedDatabaseWhenTheDownloadFails()
    {
        using var ws = new TempWorkspace();
        File.WriteAllText(Path.Combine(ws.Root, "redump-PS1.dat"), SampleDat);
        var png = Png(ws.Root);
        var web = new FakeWeb().Serve(BoxArt + "Metal%20Gear%20Solid%20%28USA%29.png", png); // the database URL answers 404
        using var client = new HttpClient(web);

        var found = await CoverSources.FindCoverAsync("PS1", "SLUS-00594", "", ws.Root, client);

        Assert.Equal(CoverSource.NameDatabase, found?.Source);
    }

    /// <summary>
    /// Real network check: runs only when the PSFPKG_LIVE environment variable is 1 (skipped in normal test runs).
    /// It downloads the real Redump database and a real box art.
    /// </summary>
    [Fact]
    public async Task Live_FindsTheCoverOfAPS1GameFromItsId()
    {
        if (Environment.GetEnvironmentVariable("PSFPKG_LIVE") != "1") return;
        using var ws = new TempWorkspace();
        var found = await CoverSources.FindCoverAsync("PS1", "SLES-00857", "Adidas Power Soccer 2", ws.Root);
        Assert.NotNull(found);
        var size = ImageConverter.GetSize(WriteTemp(ws, found.Image));
        Assert.NotNull(size);
        Assert.True(size.Value.Width > 0 && size.Value.Height > 0);
    }

    private static string WriteTemp(TempWorkspace ws, byte[] image)
    {
        var path = Path.Combine(ws.Root, "live.png");
        File.WriteAllBytes(path, image);
        return path;
    }
}
