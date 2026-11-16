using LibOrbisPkg.SFO;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class SfoBuilderTests
{
    [Fact]
    public void ContentIds_FollowTheNamingOfEachPlatform()
    {
        Assert.Equal("UP9000-SLUS00594_00-SLUS00594PS1FPKG", SfoBuilder.PS1ContentID("SLUS00594"));
        Assert.Equal("UP9000-ULUS10041_00-ULUS10041PSPFPKG", SfoBuilder.PSPContentID("ULUS10041"));
        Assert.Equal("UP9000-SLUS21258_00-SLUS212580000001", SfoBuilder.PS2ContentID("SLUS21258", "SLUS_212.58"));
    }

    [Fact]
    public void Build_WritesAValidGameDataParamSfo()
    {
        var bytes = SfoBuilder.Build(SfoBuilder.PS1ContentID("SLUS00594"), "Metal Gear Solid", "SLUS00594");
        var sfo = ParamSfo.Deserialize(bytes);

        Assert.Equal("gd", ((Utf8Value)sfo["CATEGORY"]).Value);
        Assert.Equal("Metal Gear Solid", ((Utf8Value)sfo["TITLE"]).Value);
        Assert.Equal("SLUS00594", ((Utf8Value)sfo["TITLE_ID"]).Value);
        Assert.Equal("UP9000-SLUS00594_00-SLUS00594PS1FPKG", ((Utf8Value)sfo["CONTENT_ID"]).Value);
        Assert.Equal("01.00", ((Utf8Value)sfo["VERSION"]).Value);
        Assert.Equal(1, ((IntegerValue)sfo["APP_TYPE"]).Value);
        Assert.Equal(5, ((IntegerValue)sfo["PARENTAL_LEVEL"]).Value);
    }

    [Fact]
    public void Build_AddsRemotePlayKeyAssignOnlyWhenRequested()
    {
        var without = ParamSfo.Deserialize(SfoBuilder.Build("UP9000-A_00-B", "T", "SLUS00594"));
        var with = ParamSfo.Deserialize(SfoBuilder.Build("UP9000-A_00-B", "T", "SLUS00594", true));

        Assert.DoesNotContain(without.Values, v => v.Name == "REMOTE_PLAY_KEY_ASSIGN");
        Assert.Contains(with.Values, v => v.Name == "REMOTE_PLAY_KEY_ASSIGN");
    }

    [Fact]
    public void Build_KeepsUnicodeTitles()
    {
        var sfo = ParamSfo.Deserialize(SfoBuilder.Build("UP9000-A_00-B", "ファイナルファンタジー", "SLPS00001"));
        Assert.Equal("ファイナルファンタジー", ((Utf8Value)sfo["TITLE"]).Value);
    }
}

public class Gp4EditorTests
{
    private const string Sample =
        "<?xml version=\"1.1\" encoding=\"utf-8\"?>\r\n<psproject>\r\n  <scenarios default_id=\"1\">\r\n  </scenarios>\r\n" +
        "  <files>\r\n    <file targ_path=\"eboot.bin\" orig_path=\"eboot.bin\"/>\r\n  </files>\r\n</psproject>";

    [Fact]
    public void NormalizeHeader_DowngradesXmlVersionAndSelectsScenarioZero()
    {
        var result = Gp4Editor.NormalizeHeader(Sample);
        Assert.Contains("<?xml version=\"1.0\"", result);
        Assert.DoesNotContain("version=\"1.1\"", result);
        Assert.Contains("<scenarios default_id=\"0\">", result);
    }

    [Fact]
    public void AddFiles_InsertsEntriesBeforeTheClosingFilesTag()
    {
        var entries = new[]
        {
            new Gp4FileEntry("image/disc01.iso", @"C:\games\disc 1.iso", true),
            new Gp4FileEntry("image/disc02.iso", @"C:\games\disc2.iso", false),
        };

        var result = Gp4Editor.AddFiles(Sample, entries);

        var xml = System.Xml.Linq.XDocument.Parse(Gp4Editor.NormalizeHeader(result));
        var files = xml.Descendants("file").ToList();
        Assert.Equal(3, files.Count);
        Assert.Equal("image/disc01.iso", files[1].Attribute("targ_path")!.Value);
        Assert.Equal(@"C:\games\disc 1.iso", files[1].Attribute("orig_path")!.Value);
        Assert.Equal("enable", files[1].Attribute("pfs_compression")!.Value);
        Assert.Equal("disable", files[2].Attribute("pfs_compression")!.Value);
    }

    [Fact]
    public void AddFiles_WithoutEntriesKeepsTheProjectValid()
    {
        var result = Gp4Editor.AddFiles(Sample, Array.Empty<Gp4FileEntry>());
        Assert.Single(System.Xml.Linq.XDocument.Parse(Gp4Editor.NormalizeHeader(result)).Descendants("file"));
    }

    [Fact]
    public void PatchFile_EditsTheFileInPlace()
    {
        using var ws = new TempWorkspace();
        var path = ws.Write("project.gp4", Sample);

        Gp4Editor.PatchFile(path, new[] { new Gp4FileEntry("data/disc1.bin", "game.bin", true) });

        var text = File.ReadAllText(path);
        Assert.Contains("<?xml version=\"1.0\"", text);
        Assert.Contains("data/disc1.bin", text);
        System.Xml.Linq.XDocument.Parse(text); // must still be well formed
    }
}

public class EmulatorConfigTests
{
    private const string NL = "\r\n";

    [Fact]
    public void PS1_MinimalConfig()
    {
        var text = EmulatorConfigs.BuildPS1(new PS1ConfigOptions { Upscaling = "4" });

        Assert.Equal(
            "--ps4-trophies=0" + NL + "--ps5-uds=0" + NL + "--trophies=0" + NL +
            "--image=\"data/disc1.bin\"" + NL + "--scale=4" + NL, text);
    }

    [Fact]
    public void PS1_FullConfigKeepsTheOriginalOrder()
    {
        var text = EmulatorConfigs.BuildPS1(new PS1ConfigOptions
        {
            DiscNumbers = new[] { 1, 2, 4 },
            LibcryptValue = "03E7",
            UseLuaConfig = true,
            NPTitle = "SLUS00594",
            Upscaling = "2",
            SkipBootlogo = true,
            Guncon = true,
            Force60Hz = true,
            EmulateAnalogSticks = true,
            UserConfigText = "--custom=1",
        });

        Assert.Equal(
            "--ps4-trophies=0" + NL + "--ps5-uds=0" + NL + "--trophies=0" + NL +
            "--image=\"data/disc1.bin\"" + NL + "--image=\"data/disc2.bin\"" + NL + "--image=\"data/disc4.bin\"" + NL +
            "--libcrypt=03E7" + NL +
            "--ps1-title-id=SLUS00594" + NL +
            "--scale=2" + NL +
            "--bios-hide-sce-osd=1" + NL + "--guncon" + NL + "--gpu-scanout-fps-override=60" + NL + "--sim-analog-pad=0x2020" + NL +
            "#User imported config" + NL + "--custom=1" + NL, text);
    }

    [Fact]
    public void PS2_MinimalConfig()
    {
        var text = EmulatorConfigs.BuildPS2(new PS2ConfigOptions
        {
            GameID = "SLUS-21258",
            DiscCount = 2,
            Uprender = "2x2",
            Upscaling = "4X",
            DisplayMode = "Normal",
        });

        Assert.Equal(
            "--path-vmc=\"/tmp/vmc\"" + NL + "--config-local-lua=\"\"" + NL + "--ps2-title-id=SLUS-21258" + NL +
            "--max-disc-num=2" + NL + "--gs-uprender=2x2" + NL + "--gs-upscale=4x" + NL +
            "--host-audio=1" + NL + "--rom=\"PS20220WD20050620.crack\"" + NL + "--verbose-cdvd-reads=0" + NL +
            "--host-display-mode=normal" + NL, text);
    }

    [Fact]
    public void PS2_DisableDiscResetAndMultitap()
    {
        var text = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { RestartEmulatorOnDiscChange = false, MultitapIndex = 3 });

        Assert.Contains(NL + "#Disable emu reset on disc change" + NL + "--switch-disc-reset=0", text);
        Assert.Contains(NL + "#Enable Multitap" + NL + "--mtap1=always" + NL + "--mtap2=always", text);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(1, "--mtap1=always")]
    [InlineData(2, "--mtap2=always")]
    public void PS2_MultitapPorts(int index, string expected)
    {
        var text = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { MultitapIndex = index });
        if (expected == null)
            Assert.DoesNotContain("mtap", text);
        else
            Assert.Contains(expected, text);
    }

    [Fact]
    public void PS2_ClosesTheQuoteOfThePatchesPath()
    {
        // Regression: the original code wrote --path-patches="/app0/patches without the closing quote.
        var text = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { UsePatchesPath = true });
        Assert.Contains("--path-patches=\"/app0/patches\"", text);
    }

    [Fact]
    public void PS2_EmulatorFixesAreAlwaysAppended()
    {
        // Regression: the fixes were lost when the database TXT config checkbox was used.
        var withDatabase = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { FixGraphics = true, DatabaseConfigText = "--db=1", RawGameID = "SLUS_212.58" });
        var withUser = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { FixGraphics = true, UserConfigText = "--user=1" });
        var plain = EmulatorConfigs.BuildPS2(new PS2ConfigOptions { FixGraphics = true });

        foreach (var text in new[] { withDatabase, withUser, plain })
            Assert.Contains("#Fix Graphics" + NL + "--fpu-no-clamping=0", text);

        Assert.Contains(NL + "#SLUS_212.58" + NL + "--db=1", withDatabase);
        Assert.EndsWith(NL + "#User Config" + NL + "--user=1", withUser);
    }

    [Fact]
    public void PS2_OptionalFeaturesAreWrittenInTheOriginalOrder()
    {
        var text = EmulatorConfigs.BuildPS2(new PS2ConfigOptions
        {
            MultitapIndex = 1,
            UsePS3Config = true,
            UseWidescreenPatch = true,
            UsePatchesPath = true,
            DisableMTVU = true,
        });

        var order = new[] { "--mtap1=always", "--lopnor-config=1", "--path-trophydata=\"/app0/trophy_data\"", "--path-patches=", "--vu1=jit-sync" };
        var positions = order.Select(o => text.IndexOf(o, StringComparison.Ordinal)).ToArray();
        Assert.All(positions, p => Assert.True(p >= 0));
        Assert.Equal(positions.OrderBy(p => p), positions);
    }

    [Fact]
    public void PS2_Fixes_DocumentedTypoIsPreserved()
    {
        // The original project writes "-vu0-opt-flags" with a single dash. It is kept until it can be verified on a console.
        var fixes = EmulatorConfigs.BuildPS2Fixes(new PS2ConfigOptions { ImproveSpeed = true });
        Assert.Contains(NL + "-vu0-opt-flags=1" + NL, fixes);
        Assert.Contains("--vu1-opt-flags=1", fixes);
    }

    [Fact]
    public void PSP_Config()
    {
        Assert.Equal(
            "--ps4-trophies=0" + NL + "--ps5-uds=0" + NL + "--trophies=0" + NL + "--image=\"data/USER_L0.IMG\"" + NL +
            "--antialias=SSAA4x" + NL + "--multisaves=true" + NL + "--notrophies=true" + NL + NL,
            EmulatorConfigs.BuildPSP());

        Assert.EndsWith("--notrophies=true" + NL + NL + "--custom=1", EmulatorConfigs.BuildPSP("--custom=1"));
    }
}
