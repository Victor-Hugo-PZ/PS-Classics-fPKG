using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>Restores English after a test that switches the language (the localizer is static).</summary>
public sealed class LanguageScope : IDisposable
{
    public LanguageScope(string language) => Localizer.Language = language;
    public void Dispose() => Localizer.Language = Localizer.English;
}

public class LocalizerTests
{
    [Fact]
    public void T_ReturnsEnglishWhenTheLanguageIsEnglishOrTheTextIsUnknown()
    {
        Localizer.Language = Localizer.English;
        Assert.Equal("Build fPKG", Localizer.T("Build fPKG"));
        using var _ = new LanguageScope(Localizer.Portuguese);
        Assert.Equal("Some text without translation 123", Localizer.T("Some text without translation 123"));
    }

    [Fact]
    public void T_TranslatesToPortuguese_AndKeepsLineBreaks()
    {
        using var _ = new LanguageScope(Localizer.Portuguese);
        Assert.Equal("Criar fPKG", Localizer.T("Build fPKG"));
        Assert.Equal("PKG criado!\nDeseja abrir a pasta de saída?", Localizer.T("PKG created!\r\nDo you want to open the output folder ?").Replace("\r\n", "\n"));
    }

    [Fact]
    public void F_FillsTheTranslatedFormat()
    {
        using var _ = new LanguageScope(Localizer.Portuguese);
        Assert.Equal("O ícone tem 1920×1080: ele será esticado para 512×512.", ImageConverter.DescribeStretch("The icon", 1920, 1080, 512, 512));
    }

    [Theory]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("pt-PT", "pt-BR")]
    [InlineData("pt", "pt-BR")]
    [InlineData("en-US", "en")]
    [InlineData("de-DE", "en")]
    [InlineData("", "en")]
    public void Normalize_MapsCulturesToSupportedLanguages(string culture, string expected)
    {
        Assert.Equal(expected, Localizer.Normalize(culture));
    }

    [Fact]
    public void TranslateProperty_RemembersTheEnglishSource_AndFollowsTextsChangedByTheApp()
    {
        var owner = new object();
        using (new LanguageScope(Localizer.Portuguese))
        {
            Assert.Equal("Criar fPKG", Localizer.TranslateProperty(owner, "Text", "Build fPKG"));
            // Translating again (the control still shows the translation) keeps the source
            Assert.Equal("Criar fPKG", Localizer.TranslateProperty(owner, "Text", "Criar fPKG"));
        }
        // Back to English: the original text comes back
        Assert.Equal("Build fPKG", Localizer.TranslateProperty(owner, "Text", "Criar fPKG"));
        // The app changed the text: the new text becomes the source
        Assert.Equal("Guide", Localizer.TranslateProperty(owner, "Text", "Guide"));
    }

    [Fact]
    public void ParseTable_IgnoresCommentsAndEmptyLines()
    {
        var pairs = Localizer.ParseTable("# comment\n\nHello\tOlá\nNo tab here\nA\\nB\tC\\nD\n");
        Assert.Equal(new[] { "Hello", "A\\nB" }, pairs.Select(p => p.Key));
        Assert.Equal("C\\nD", pairs[1].Value);
    }

    [Fact]
    public void PortugueseTable_HasNoDuplicateKeysAndKeepsPlaceholders()
    {
        var file = Path.Combine(TestEnvironment.RepoRoot, "Shared", "PSClassics.Core", "Localization", "pt-BR.tsv");
        var pairs = Localizer.ParseTable(File.ReadAllText(file));
        var duplicates = pairs.GroupBy(p => p.Key).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicates.Count == 0, "Duplicate keys: " + string.Join(" | ", duplicates));

        var placeholder = new System.Text.RegularExpressions.Regex(@"\{\d+\}");
        var broken = pairs.Where(p => !placeholder.Matches(p.Key).Select(m => m.Value).OrderBy(v => v)
                                         .SequenceEqual(placeholder.Matches(p.Value).Select(m => m.Value).OrderBy(v => v)))
                          .Select(p => p.Key).ToList();
        Assert.True(broken.Count == 0, "Placeholders differ: " + string.Join(" | ", broken));
        Assert.DoesNotContain(pairs, p => string.IsNullOrWhiteSpace(p.Value));
    }
}

public class AppSettingsTests
{
    [Fact]
    public void SaveAndLoad_RoundTrip_AndDamagedFilesGiveDefaults()
    {
        using var ws = new TempWorkspace();
        var file = Path.Combine(ws.Root, "settings.json");
        new AppSettings { Language = "pt-BR", GuidedMode = false, CheckForUpdates = false, SkippedVersion = "9.9.9" }.Save(file);

        var loaded = AppSettings.Load(file);
        Assert.Equal("pt-BR", loaded.Language);
        Assert.False(loaded.GuidedMode);
        Assert.False(loaded.CheckForUpdates);
        Assert.Equal("9.9.9", loaded.SkippedVersion);

        File.WriteAllText(file, "{ not json");
        Assert.True(AppSettings.Load(file).GuidedMode);
        Assert.True(AppSettings.Load(Path.Combine(ws.Root, "missing.json")).CheckPackageAfterBuild);
    }

    [Fact]
    public void EffectiveLanguage_UsesTheChoiceOrTheSystemLanguage()
    {
        Assert.Equal("pt-BR", new AppSettings { Language = "pt-BR" }.EffectiveLanguage());
        Assert.Equal(Localizer.SystemLanguage(), new AppSettings { Language = "" }.EffectiveLanguage());
    }
}

public class EmulatorPresetsTests
{
    // The preset values must be items of the combo boxes of both apps, or applying them would select nothing.
    private static readonly string[] PS1Upscaling = { "6", "4", "2", "1" };
    private static readonly string[] PS2Emulators = { "Jak v2", "Rogue v1" };
    private static readonly string[] PS2Upscaling = { "None", "GPU", "Bilinear", "Edgesmooth", "Point" };
    private static readonly string[] PS2Uprender = { "None", "2x2" };
    private static readonly string[] PS2DisplayModes = { "Normal", "Full", "4:3", "16:9" };

    [Fact]
    public void Presets_UseValuesOfTheComboBoxes()
    {
        Assert.All(EmulatorPresets.PS1, p => Assert.Contains(p.Upscaling, PS1Upscaling));
        Assert.All(EmulatorPresets.PS2, p =>
        {
            Assert.Contains(p.Emulator, PS2Emulators);
            Assert.Contains(p.Upscaling, PS2Upscaling);
            Assert.Contains(p.Uprender, PS2Uprender);
            Assert.Contains(p.DisplayMode, PS2DisplayModes);
        });
    }

    [Fact]
    public void RecommendedPresets_AreTheDefaultsOfTheForm()
    {
        var ps1 = EmulatorPresets.PS1[0];
        Assert.Equal("Recommended", ps1.Name);
        Assert.Equal("2", ps1.Upscaling);
        var ps2 = EmulatorPresets.PS2[0];
        Assert.Equal(("Jak v2", "None", "2x2", "Normal", true, true), (ps2.Emulator, ps2.Upscaling, ps2.Uprender, ps2.DisplayMode, ps2.FixGraphics, ps2.UseCompression));
    }

    [Fact]
    public void EmulatorFoldersOfThePresetsExistInTools()
    {
        if (!TestEnvironment.ToolsAvailable) return;
        Assert.All(EmulatorPresets.PS2, p => Assert.True(Directory.Exists(Path.Combine(TestEnvironment.ToolsDirectory, "PS4", "emus", p.Emulator)), p.Emulator));
    }
}

public class BuildPreflightTests
{
    [Theory]
    [InlineData("SLUS00594", 0, false)]
    [InlineData("", 1, true)]
    [InlineData("SLUS0059", 1, true)]
    [InlineData("SLUS_0059", 1, true)]
    [InlineData("slus00594", 1, true)]
    [InlineData("ABCDEFGHI", 1, false)] // valid characters, unusual pattern: only a warning
    public void CheckNPTitle(string npTitle, int count, bool isError)
    {
        var issues = BuildPreflight.CheckNPTitle(npTitle);
        Assert.Equal(count, issues.Count);
        if (count > 0) Assert.Equal(isError, issues[0].IsError);
    }

    [Fact]
    public void PS1_ReportsMissingDiscCueAndTitle()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write("game/game.bin", new byte[2352]);
        var issues = BuildPreflight.CheckPS1(new PS1BuildRequest { Disc1 = bin, Disc2 = Path.Combine(ws.Root, "missing.bin"), NPTitle = "SLUS00594" }, ws.Root);

        Assert.Contains(issues, i => i.IsError && i.Message.Contains(".CUE file of disc 1"));
        Assert.Contains(issues, i => i.IsError && i.Message.StartsWith("Disc 2 was not found"));
        Assert.Contains(issues, i => i.IsError && i.Message == "No game title specified.");
        Assert.Contains(issues, i => i.IsError && i.Message.StartsWith("The Tools folder was not found"));
        Assert.All(issues, i => Assert.False(string.IsNullOrEmpty(i.Suggestion)));
    }

    [Fact]
    public void PS1_CompleteRequestHasNoErrorsExceptTools()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write("game/game.bin", new byte[2352]);
        ws.Write("game/game.cue", "FILE \"game.bin\" BINARY");
        var icon = TestImages.Save(Path.Combine(ws.Root, "icon.png"), 64, 64);
        var issues = BuildPreflight.CheckPS1(new PS1BuildRequest { Disc1 = bin, Title = "Game", NPTitle = "SLUS00594", IconFile = icon }, ws.Root);
        Assert.Equal(new[] { "The Tools folder was not found" }, issues.Select(i => i.Message.Split(" at ")[0]));
    }

    [Fact]
    public void PS2_ReportsUnreadableImagesAndOddMemoryCards()
    {
        using var ws = new TempWorkspace();
        var iso = ws.Write("game.iso", new byte[4096]);
        var image = ws.Write("icon.png", "not an image");
        var card = ws.Write("card.vm2", new byte[1024]);
        var issues = BuildPreflight.CheckPS2(new PS2BuildRequest { Disc1 = iso, Title = "T", NPTitle = "SLUS21258", IconFile = image, MemoryCardFile = card }, ws.Root);

        Assert.Contains(issues, i => i.IsError && i.Message == "The icon could not be read.");
        Assert.Contains(issues, i => !i.IsError && i.Message.StartsWith("The memory card is"));
    }

    [Fact]
    public void PSP_WarnsAboutAMissingIcon()
    {
        using var ws = new TempWorkspace();
        var iso = ws.Write("game.iso", new byte[4096]);
        var issues = BuildPreflight.CheckPSP(new PSPBuildRequest { IsoFile = iso, Title = "T", NPTitle = "ULUS10041" }, ws.Root);
        Assert.Contains(issues, i => !i.IsError && i.Message.StartsWith("No icon selected"));
    }

    [Fact]
    public void Describe_ListsErrorsFirstWithTheirFixes()
    {
        var text = BuildPreflight.Describe(new[]
        {
            new PreflightIssue { IsError = false, Message = "W", Suggestion = "fix w" },
            new PreflightIssue { IsError = true, Message = "E", Suggestion = "fix e" },
        });
        Assert.Equal("✘ E\n   → fix e\n⚠ W\n   → fix w", text);
    }

    [Theory]
    [InlineData(512L * 1024 * 1024, "512 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1.5 GB")]
    public void FormatSize(long bytes, string expected)
    {
        Assert.Equal(expected, BuildPreflight.FormatSize(bytes).Replace(',', '.'));
    }
}

public class ErrorExplainerTests
{
    [Theory]
    [InlineData("wine: Unhandled exception 0x0eedfade in thread 24", "Wine could not open a window (no display was found).")]
    [InlineData("err:module:import_dll Library MSVCR90.dll not found", "The Visual C++ 2008 runtime is missing in Wine.")]
    [InlineData("System.IO.IOException: There is not enough space on the disk.", "The disk is full.")]
    [InlineData("Access to the path 'C:\\Program Files\\x' is denied.", "The app is not allowed to write to a folder.")]
    [InlineData("The GP4 project was not generated by gengp4_patch.exe.", "The project generator (gengp4) failed.")]
    [InlineData("[Error] something unexpected", "The publishing tool stopped with an error.")]
    public void Explain_RecognizesCommonFailures(string output, string explanation)
    {
        var result = ErrorExplainer.Explain(output);
        Assert.Equal(explanation, result.Explanation);
        Assert.NotEqual("", result.Suggestion);
    }

    [Fact]
    public void FindKeyLine_ReturnsTheFirstErrorLine()
    {
        Assert.Equal("[Error] Disk full", ErrorExplainer.FindKeyLine("Starting\n[Info] ok\n[Error] Disk full\nmore"));
        Assert.Equal("", ErrorExplainer.FindKeyLine("all good"));
    }
}

public class BuildHistoryTests
{
    [Fact]
    public void AddOrUpdate_KeepsNewestFirst_AndReplacesEntriesWithTheSameId()
    {
        using var ws = new TempWorkspace();
        var file = Path.Combine(ws.Root, "history.json");
        var first = new HistoryEntry { Title = "A", CreatedAt = new DateTime(2026, 1, 1) };
        var second = new HistoryEntry { Title = "B", CreatedAt = new DateTime(2026, 1, 2) };
        BuildHistory.AddOrUpdate(first, file);
        BuildHistory.AddOrUpdate(second, file);
        first.ConsoleResult = ConsoleResult.Crashes;
        first.Notes = "crashes after the menu";
        BuildHistory.AddOrUpdate(first, file);

        var entries = BuildHistory.Load(file);
        Assert.Equal(new[] { "B", "A" }, entries.Select(e => e.Title));
        Assert.Equal(ConsoleResult.Crashes, entries[1].ConsoleResult);
        Assert.Equal("crashes after the menu", entries[1].Notes);
        Assert.Contains("\"Crashes\"", File.ReadAllText(file));

        BuildHistory.Remove(second.Id, file);
        Assert.Single(BuildHistory.Load(file));
    }

    [Fact]
    public void Summarize_ListsEmulatorAndChangedOptions()
    {
        var text = BuildHistory.Summarize(new PS2BuildRequest
        {
            Emulator = "Rogue v1", Upscaling = "None", Uprender = "2x2", DisplayMode = "Normal",
            FixGraphics = true, UseCompression = true, Disc1 = "a.iso", Disc2 = "b.iso",
        });
        Assert.Equal("Emulator: Rogue v1\nUpscaling: None\nUprender: 2x2\nDisplay mode: Normal\nDiscs: 2\nFix graphics", text);
        Assert.Equal(BuildHistory.ConsoleResultNames.Count, Enum.GetValues<ConsoleResult>().Length);
    }
}

public class CoverArtTests
{
    [Theory]
    [InlineData("SLUS_212.58", "SLUS-21258")]
    [InlineData("SLUS-00594", "SLUS-00594")]
    [InlineData("slus00594", "SLUS-00594")]
    [InlineData("ABC", "")]
    [InlineData("", "")]
    public void NormalizeSerial(string input, string expected)
    {
        Assert.Equal(expected, CoverArt.NormalizeSerial(input));
    }

    [Fact]
    public void CoverUrl_OnlyForPS1AndPS2()
    {
        Assert.EndsWith("/psx-covers/main/covers/default/SLUS-00594.jpg", CoverArt.CoverUrl("PS1", "SLUS-00594"));
        Assert.EndsWith("/ps2-covers/main/covers/default/SLUS-21258.jpg", CoverArt.CoverUrl("PS2", "SLUS-21258"));
        Assert.Equal("", CoverArt.CoverUrl("PSP", "ULUS-10041"));
    }

    [Fact]
    public void CreateImages_MakesASquareIconAnd16By9Background()
    {
        using var ws = new TempWorkspace();
        var cover = File.ReadAllBytes(TestImages.Save(Path.Combine(ws.Root, "cover.jpg"), 300, 420, "jpg", 200, 30, 30));
        var images = CoverArt.CreateImages(cover, "SLUS-00594", null, ws.Root);

        Assert.Equal((512, 512), ImageConverter.GetSize(images.IconFile).Value);
        Assert.Equal((1920, 1080), ImageConverter.GetSize(images.BackgroundFile).Value);
        // The images are valid input for the build (converted to 24 bit PNG)
        Assert.NotEmpty(ImageConverter.ToPng24(images.IconFile, 512, 512));
    }

    [Fact]
    public void CreateImagesFromPspIso_UsesIcon0AndPic1()
    {
        using var ws = new TempWorkspace();
        var icon = File.ReadAllBytes(TestImages.Save(Path.Combine(ws.Root, "ICON0.PNG"), 144, 80));
        var pic = File.ReadAllBytes(TestImages.Save(Path.Combine(ws.Root, "PIC1.PNG"), 480, 272));
        var iso = Path.Combine(ws.Root, "psp.iso");
        var builder = new CDBuilder { UseJoliet = false, VolumeIdentifier = "PSP" };
        builder.AddFile("PSP_GAME\\PARAM.SFO", new byte[16]);
        builder.AddFile("PSP_GAME\\ICON0.PNG", icon);
        builder.AddFile("PSP_GAME\\PIC1.PNG", pic);
        builder.Build(iso);

        var images = CoverArt.CreateImagesFromPspIso(iso, "ULUS10041", Path.Combine(ws.Root, "out"));
        Assert.NotNull(images);
        Assert.Equal((512, 512), ImageConverter.GetSize(images.IconFile).Value);
        Assert.Equal((1920, 1080), ImageConverter.GetSize(images.BackgroundFile).Value);

        Assert.Null(CoverArt.CreateImagesFromPspIso(ws.Write("empty.iso", new byte[4096]), "X", ws.Root));
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v2.2.0", "2.2.0")]
    [InlineData("2.2", "2.2")]
    [InlineData("v3", "3.0")]
    [InlineData("v2.3.0-beta", "2.3.0")]
    public void ParseVersion(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseVersion(tag));
    }

    [Theory]
    [InlineData("v2.3.0", "2.2.0", true)]
    [InlineData("v2.2.0", "2.2.0", false)]
    [InlineData("v2.2.0", "2.2.0.0", false)]
    [InlineData("v2.1.0", "2.2.0", false)]
    public void ParseLatestRelease_ComparesWithTheCurrentVersion(string tag, string current, bool newer)
    {
        var json = "{\"tag_name\":\"" + tag + "\",\"html_url\":\"https://github.com/x/y/releases/tag/" + tag + "\"}";
        var info = UpdateChecker.ParseLatestRelease(json, Version.Parse(current));
        Assert.Equal(newer, info.IsNewer);
        Assert.EndsWith(tag, info.Url);
    }

    [Fact]
    public void IsCheckDue_AtMostOncePerDayAndOnlyWhenEnabled()
    {
        var now = new DateTime(2026, 11, 17, 12, 0, 0);
        Assert.True(UpdateChecker.IsCheckDue(new AppSettings(), now));
        Assert.False(UpdateChecker.IsCheckDue(new AppSettings { LastUpdateCheck = now.AddHours(-2) }, now));
        Assert.False(UpdateChecker.IsCheckDue(new AppSettings { CheckForUpdates = false }, now));
    }
}

public class DroppedFilesTests
{
    [Fact]
    public void Classify_RecognizesGamesImagesConfigsAndPackages()
    {
        using var ws = new TempWorkspace();
        var bin = ws.Write("game.bin", new byte[2352 * 10]);
        var cue = ws.Write("game.cue", "FILE \"game.bin\" BINARY");
        var card = ws.Write("card.bin", new byte[8 * 1024 * 1024 + 16384]);
        var ps2 = Path.Combine(ws.Root, "ps2.iso");
        new CDBuilder { VolumeIdentifier = "PS2" }.Also(b => b.AddFile("SYSTEM.CNF", new byte[8])).Build(ps2);
        var psp = Path.Combine(ws.Root, "psp.iso");
        new CDBuilder { VolumeIdentifier = "PSP" }.Also(b => b.AddFile("PSP_GAME\\PARAM.SFO", new byte[8])).Build(psp);
        var square = TestImages.Save(Path.Combine(ws.Root, "square.png"), 100, 100);
        var wide = TestImages.Save(Path.Combine(ws.Root, "wide.jpg"), 160, 90, "jpg");

        Assert.Equal(DroppedFileKind.PS1Disc, DroppedFiles.Classify(bin));
        Assert.Equal(DroppedFileKind.PS1Disc, DroppedFiles.Classify(cue));
        Assert.Equal(DroppedFileKind.MemoryCard, DroppedFiles.Classify(card));
        Assert.Equal(DroppedFileKind.PS2Disc, DroppedFiles.Classify(ps2));
        Assert.Equal(DroppedFileKind.PSPDisc, DroppedFiles.Classify(psp));
        Assert.Equal(DroppedFileKind.Icon, DroppedFiles.Classify(square));
        Assert.Equal(DroppedFileKind.Background, DroppedFiles.Classify(wide));
        Assert.Equal(DroppedFileKind.TxtConfig, DroppedFiles.Classify(ws.Write("c.txt", "x")));
        Assert.Equal(DroppedFileKind.LuaConfig, DroppedFiles.Classify(ws.Write("c.lua", "x")));
        Assert.Equal(DroppedFileKind.MemoryCard, DroppedFiles.Classify(ws.Write("c.vm2", "x")));
        Assert.Equal(DroppedFileKind.Package, DroppedFiles.Classify(ws.Write("g.pkg", "x")));
        Assert.Equal(DroppedFileKind.Unknown, DroppedFiles.Classify(ws.Write("readme.md", "x")));
        Assert.Equal(DroppedFileKind.Unknown, DroppedFiles.Classify(Path.Combine(ws.Root, "missing.iso")));

        Assert.Equal(bin, DroppedFiles.DiscFile(cue));
        Assert.Equal(bin, DroppedFiles.DiscFile(bin));
    }
}

internal static class FluentExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
