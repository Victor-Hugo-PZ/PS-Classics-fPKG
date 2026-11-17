using System.Text.RegularExpressions;
using System.Xml.Linq;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

/// <summary>
/// Every text the user can see in either app (XAML of the Windows and Linux/macOS apps, and the texts passed to
/// Localizer.T / Localizer.F in code) must have a Portuguese translation, so both apps are fully translated.
/// </summary>
public class TranslationCoverageTests
{
    // Texts that stay the same in every language (names, imitation of the console screen, technical values)
    private static readonly HashSet<string> NotTranslated = new(StringComparer.Ordinal)
    {
        "PS Classics fPKG Builder", "PS4", "PS5", "PS1 Classics", "PS2 Classics", "PSP Classics",
        "PlayStation 1", "PlayStation 2", "PlayStation Portable", "Original version by Victor",
        "icon0.png · 512×512", "GunCon", "Disable MTVU", "NP Title", "TXT config", "LUA config", "fPKG",
    };

    private static readonly string[] TextAttributes =
    {
        "Text", "Content", "Header", "PlaceholderText", "ToolTip", "ToolTip.Tip", "LabelText", "HelpText", "Title",
    };

    private static string Root => TestEnvironment.RepoRoot;

    private static IEnumerable<string> XamlFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "Windows", "PS Classics fPKG Builder"), "*.xaml")
            .Concat(Directory.EnumerateFiles(Path.Combine(Root, "CrossPlatform", "PSClassicsfPKGBuilder"), "*.axaml", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !Path.GetFileName(f).StartsWith("DashPreview") && !Path.GetFileName(f).StartsWith("App") && !Path.GetFileName(f).StartsWith("Application") && !Path.GetFileName(f).StartsWith("Styles"));

    /// <summary>Texts shown by the XAML of both apps (combo box items keep their values, they are not translated).</summary>
    public static IEnumerable<string> XamlTexts()
    {
        foreach (var file in XamlFiles())
        {
            var document = XDocument.Load(file);
            foreach (var element in document.Descendants())
            {
                if (element.Name.LocalName == "ComboBoxItem") continue;
                foreach (var attribute in element.Attributes())
                {
                    if (!TextAttributes.Contains(attribute.Name.LocalName)) continue;
                    var value = attribute.Value;
                    if (value.StartsWith("{") || !value.Any(char.IsLetter)) continue;
                    yield return value;
                }
            }
        }
    }

    private static readonly Regex VbCall = new(@"(?<![A-Za-z0-9_])(?:Localizer\.)?[TF]\(""((?:[^""]|"""")*)""\s*[,)]");
    private static readonly Regex CsCall = new(@"(?<![A-Za-z0-9_.])(?:Localizer\.)?[TF]\(""((?:[^""\\]|\\.)*)""\s*[,)]");

    /// <summary>Literal texts passed to Localizer.T / Localizer.F in the Core and in both apps.</summary>
    public static IEnumerable<string> CodeTexts()
    {
        var vbFolders = new[] { Path.Combine(Root, "Shared", "PSClassics.Core"), Path.Combine(Root, "Windows", "PS Classics fPKG Builder") };
        foreach (var file in vbFolders.SelectMany(d => Directory.EnumerateFiles(d, "*.vb", SearchOption.AllDirectories)))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in VbCall.Matches(File.ReadAllText(file)))
                yield return m.Groups[1].Value.Replace("\"\"", "\"");
        }
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "CrossPlatform", "PSClassicsfPKGBuilder"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            foreach (Match m in CsCall.Matches(File.ReadAllText(file)))
                yield return Regex.Unescape(m.Groups[1].Value);
        }
    }

    /// <summary>Texts built from parts or kept in data (presets, error explanations, history results...).</summary>
    public static IEnumerable<string> DataTexts()
    {
        foreach (var preset in EmulatorPresets.PS1) { yield return preset.Name; yield return preset.Description; }
        foreach (var preset in EmulatorPresets.PS2) { yield return preset.Name; yield return preset.Description; }
        foreach (var text in ErrorExplainer.AllTexts()) yield return text;
        foreach (var text in BuildHistory.ConsoleResultNames) yield return text;
        foreach (var image in new[] { "The icon", "The background" })
        {
            yield return image + " could not be read.";
            yield return image + " was not found: {0}";
            yield return image + " is {0}×{1}: it will be stretched to {2}×{3}.";
            yield return image + " is {0}×{1}: it will be upscaled to {2}×{3} and may look blurry.";
        }
    }

    [Fact]
    public void EveryVisibleText_HasAPortugueseTranslation()
    {
        var missing = XamlTexts().Concat(CodeTexts()).Concat(DataTexts())
            .Where(text => !NotTranslated.Contains(text))
            .Where(text => !Localizer.HasTranslation(text, Localizer.Portuguese))
            .Select(Localizer.KeyOf)
            .Distinct()
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0, $"{missing.Count} text(s) without a Portuguese translation:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void TheScanFindsTextsInBothApps()
    {
        var xaml = XamlTexts().ToList();
        Assert.Contains("Build fPKG", xaml);
        Assert.Contains("Home screen preview", xaml);
        Assert.True(xaml.Count > 150, "Too few XAML texts found: " + xaml.Count);
    }
}
