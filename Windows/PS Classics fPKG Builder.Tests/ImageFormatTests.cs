using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class ImageFormatsTests
{
    [Theory]
    [InlineData("icon.png", true)]
    [InlineData("icon.PNG", true)]
    [InlineData("photo.jpg", true)]
    [InlineData("photo.jpeg", true)]
    [InlineData("scan.bmp", true)]
    [InlineData("anim.gif", true)]
    [InlineData("scan.tif", true)]
    [InlineData("scan.tiff", true)]
    [InlineData("modern.webp", false)]   // GDI+ cannot decode WebP
    [InlineData("document.pdf", false)]
    [InlineData("noextension", false)]
    [InlineData("", false)]
    public void IsSupported_AcceptsTheImagesGdiPlusCanDecode(string fileName, bool expected)
    {
        Assert.Equal(expected, ImageFormats.IsSupported(fileName));
    }

    [Fact]
    public void OpenFilter_ListsEverySupportedExtensionFirst()
    {
        var filter = ImageFormats.OpenFilter();
        var parts = filter.Split('|');

        Assert.StartsWith("Images (", parts[0]);
        foreach (var ext in ImageFormats.Extensions)
            Assert.Contains("*." + ext, parts[1]);
        Assert.DoesNotContain("webp", parts[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenFilter_HasOnePairPerFormatAfterTheCombinedEntry()
    {
        var parts = ImageFormats.OpenFilter().Split('|');
        // "Images (...)", "patterns", then pairs of "label", "patterns"
        Assert.True(parts.Length >= 4 && parts.Length % 2 == 0, "The filter must have pairs of label and patterns.");
    }
}

public class ImageLoaderFormatTests
{
    private static string Save(TempWorkspace ws, string name, ImageFormat format)
    {
        var path = Path.Combine(ws.Root, name);
        using var bitmap = new Bitmap(64, 48);
        using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.DarkBlue);
        bitmap.Save(path, format);
        return path;
    }

    [Theory]
    [InlineData("photo.jpg", "Jpeg")]
    [InlineData("scan.bmp", "Bmp")]
    [InlineData("anim.gif", "Gif")]
    [InlineData("scan.tif", "Tiff")]
    [InlineData("icon.png", "Png")]
    public void TryLoad_ReadsEveryAcceptedFormat(string name, string formatName)
    {
        using var ws = new TempWorkspace();
        var format = (ImageFormat)typeof(ImageFormat).GetProperty(formatName)!.GetValue(null)!;
        var path = Save(ws, name, format);

        var image = ImageLoader.TryLoad(path);

        Assert.NotNull(image);
        Assert.Equal(64, image.PixelWidth);
        Assert.Equal(48, image.PixelHeight);
    }

    [Fact]
    public void SavedJpegCanBeConvertedToThePng_UsedByTheBuild()
    {
        using var ws = new TempWorkspace();
        var jpg = Save(ws, "photo.jpg", ImageFormat.Jpeg);
        var png = Path.Combine(ws.Root, "icon0.png");

        // Same steps as BuilderHelpers.SavePNG: decode, resize, convert to 24 bpp, save as PNG.
        using (var source = System.Drawing.Image.FromFile(jpg))
        using (var resized = Utils.ResizeAsImage(source, 512, 512))
        using (var converted = Utils.ConvertTo24bppPNG(resized))
            converted.Save(png, ImageFormat.Png);

        using var reloaded = System.Drawing.Image.FromFile(png);
        Assert.Equal(new System.Drawing.Size(512, 512), reloaded.Size);
        Assert.Equal(ImageFormat.Png.Guid, reloaded.RawFormat.Guid);
    }
}

/// <summary>Help labels of the form (WPF controls, run on an STA thread).</summary>
public class FieldLabelTests
{
    private static void RunSta(Action action)
    {
        Exception error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw new Xunit.Sdk.XunitException("STA test failed: " + error);
    }

    [Fact]
    public void IsRequired_ShowsRequiredOrOptionalBadge()
    {
        RunSta(() =>
        {
            var label = new FieldLabel { LabelText = "Disc 1", IsRequired = true };
            Assert.Equal("Required", ((System.Windows.Controls.TextBlock)label.FindName("BadgeText")).Text);
            Assert.True(label.IsRequired);

            label.IsRequired = false;
            Assert.Equal("Optional", ((System.Windows.Controls.TextBlock)label.FindName("BadgeText")).Text);
            Assert.False(label.IsRequired);
        });
    }

    [Fact]
    public void LabelAndHelpTextAreStored()
    {
        RunSta(() =>
        {
            var label = new FieldLabel { LabelText = "Icon", HelpText = "Some help" };
            Assert.Equal("Icon", ((System.Windows.Controls.TextBlock)label.FindName("LabelTextBlock")).Text);
            Assert.Equal("Some help", label.HelpText);
        });
    }
}
