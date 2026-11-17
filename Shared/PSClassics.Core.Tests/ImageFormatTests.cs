using PS_Classics_fPKG_Builder;
using SkiaSharp;
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
    [InlineData("modern.webp", true)]
    [InlineData("scan.tif", false)]      // SkiaSharp cannot decode TIFF
    [InlineData("document.pdf", false)]
    [InlineData("noextension", false)]
    [InlineData("", false)]
    public void IsSupported_AcceptsTheImagesEveryOsCanDecode(string fileName, bool expected)
    {
        Assert.Equal(expected, ImageFormats.IsSupported(fileName));
    }

    [Fact]
    public void OpenFilter_ListsEverySupportedExtensionFirst()
    {
        var parts = ImageFormats.OpenFilter().Split('|');

        Assert.StartsWith("Images (", parts[0]);
        foreach (var ext in ImageFormats.Extensions)
            Assert.Contains("*." + ext, parts[1]);
        Assert.True(parts.Length >= 4 && parts.Length % 2 == 0, "The filter must have pairs of label and patterns.");
    }

    [Fact]
    public void Patterns_MatchTheExtensions()
    {
        Assert.Equal(ImageFormats.Extensions.Select(e => "*." + e), ImageFormats.Patterns());
    }
}

public class ImageConverterTests
{
    /// <summary>Reads width, height, bit depth and color type from the IHDR chunk of a PNG.</summary>
    private static (int Width, int Height, int BitDepth, int ColorType) PngHeader(byte[] png)
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png.Take(8));
        Assert.Equal("IHDR", System.Text.Encoding.ASCII.GetString(png, 12, 4));
        int BE(int o) => (png[o] << 24) | (png[o + 1] << 16) | (png[o + 2] << 8) | png[o + 3];
        return (BE(16), BE(20), png[24], png[25]);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("webp")]
    [InlineData("bmp")]
    public void ToPng24_ConvertsEveryFormatToA24BitPngOfTheRequestedSize(string format)
    {
        using var ws = new TempWorkspace();
        var source = TestImages.Save(ws.PathOf("source." + format), 300, 200, format);

        var png = ImageConverter.ToPng24(source, 512, 512);

        var header = PngHeader(png);
        Assert.Equal((512, 512, 8, 2), header); // 8 bit RGB, no alpha: what the PS4 tools expect
        using var decoded = SKBitmap.Decode(png);
        Assert.Equal(512, decoded.Width);
        var pixel = decoded.GetPixel(256, 256);
        Assert.InRange(pixel.Blue, 140, 180); // the color survives the conversion (JPEG/WebP are lossy)
        Assert.InRange(pixel.Red, 0, 40);
    }

    [Fact]
    public void ToPng24_TurnsTransparentAreasBlackLikeTheOriginalApp()
    {
        using var ws = new TempWorkspace();
        var source = TestImages.Save(ws.PathOf("transparent.png"), 64, 64, "png", 255, 255, 255, 0);

        using var decoded = SKBitmap.Decode(ImageConverter.ToPng24(source, 32, 32));

        var pixel = decoded.GetPixel(10, 10);
        Assert.Equal((byte)0, pixel.Red);
        Assert.Equal((byte)0, pixel.Green);
        Assert.Equal((byte)0, pixel.Blue);
        Assert.Equal((byte)255, pixel.Alpha);
    }

    [Fact]
    public void ToPng24_RejectsFilesThatAreNotImages()
    {
        using var ws = new TempWorkspace();
        var broken = ws.Write("broken.png", "not an image");

        var ex = Assert.Throws<InvalidDataException>(() => ImageConverter.ToPng24(broken, 10, 10));
        Assert.Contains("could not be read", ex.Message);
    }

    [Fact]
    public void SavePng24_WritesTheFile()
    {
        using var ws = new TempWorkspace();
        var source = TestImages.Save(ws.PathOf("bg.png"), 64, 36);
        var target = ws.PathOf("pic0.png");

        ImageConverter.SavePng24(source, target, 1920, 1080);

        Assert.Equal((1920, 1080, 8, 2), PngHeader(File.ReadAllBytes(target)));
    }

    [Fact]
    public void GetSize_ReadsTheHeaderOrReturnsNothing()
    {
        using var ws = new TempWorkspace();
        Assert.Equal((300, 200), ImageConverter.GetSize(TestImages.Save(ws.PathOf("a.png"), 300, 200)));
        Assert.Null(ImageConverter.GetSize(ws.Write("b.png", "nope")));
        Assert.Null(ImageConverter.GetSize(ws.PathOf("missing.png")));
    }

    [Fact]
    public void ToPreviewPng_KeepsTheAspectRatioAndLimitsTheWidth()
    {
        using var ws = new TempWorkspace();
        var source = TestImages.Save(ws.PathOf("wide.webp"), 1000, 500, "webp");

        var header = PngHeader(ImageConverter.ToPreviewPng(source, 200));

        Assert.Equal(200, header.Width);
        Assert.Equal(100, header.Height);
        Assert.Null(ImageConverter.ToPreviewPng(ws.Write("x.png", "nope"), 100));
    }

    [Theory]
    [InlineData(512, 512, 512, 512, "")]
    [InlineData(1024, 1024, 512, 512, "")]
    [InlineData(3840, 2160, 1920, 1080, "")]
    [InlineData(800, 600, 512, 512, "stretched")]
    [InlineData(1920, 1200, 1920, 1080, "stretched")]
    [InlineData(256, 256, 512, 512, "upscaled")]
    [InlineData(1280, 720, 1920, 1080, "upscaled")]
    public void DescribeStretch_WarnsAboutDistortionAndUpscaling(int width, int height, int targetWidth, int targetHeight, string expected)
    {
        var text = ImageConverter.DescribeStretch("The icon", width, height, targetWidth, targetHeight);
        if (expected == "")
            Assert.Equal("", text);
        else
            Assert.Contains(expected, text);
    }

    [Fact]
    public void PreviewHints_DescribeBothImagesTheSameWayOnEveryOs()
    {
        using var ws = new TempWorkspace();
        var smallIcon = TestImages.Save(ws.PathOf("icon.png"), 256, 256);
        var wideBackground = TestImages.Save(ws.PathOf("bg.png"), 1920, 600);
        var broken = ws.Write("broken.png", "nope");

        Assert.Empty(ImageConverter.PreviewHints("", ""));
        var hints = ImageConverter.PreviewHints(smallIcon, wideBackground);
        Assert.Contains(hints, h => h.Contains("upscaled"));
        Assert.Contains(hints, h => h.Contains("1920×600"));
        Assert.Contains("The icon could not be read.", ImageConverter.PreviewHints(broken, ""));
    }
}

public class PngWriterTests
{
    [Fact]
    public void EncodeRgb24_ProducesAPngThatDecodesToTheSamePixels()
    {
        var rgb = new byte[3 * 2 * 2] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 };

        using var decoded = SKBitmap.Decode(PngWriter.EncodeRgb24(rgb, 2, 2));

        Assert.Equal(new SKColor(255, 0, 0), decoded.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 255, 0), decoded.GetPixel(1, 0));
        Assert.Equal(new SKColor(0, 0, 255), decoded.GetPixel(0, 1));
        Assert.Equal(new SKColor(255, 255, 255), decoded.GetPixel(1, 1));
    }

    [Fact]
    public void EncodeRgb24_RejectsAWrongBufferSize()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.EncodeRgb24(new byte[5], 2, 2));
    }
}
