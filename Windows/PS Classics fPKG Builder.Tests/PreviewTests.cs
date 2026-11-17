using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class ImageLoaderTests
{
    private static string SavePng(TempWorkspace ws, string name, int width, int height)
    {
        var path = Path.Combine(ws.Root, name);
        using var bitmap = new Bitmap(width, height);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public void TryLoad_LoadsAFrozenBitmapWithoutLockingTheFile()
    {
        using var ws = new TempWorkspace();
        var path = SavePng(ws, "icon.png", 64, 32);

        var image = ImageLoader.TryLoad(path);

        Assert.NotNull(image);
        Assert.True(image.IsFrozen);
        Assert.Equal(64, image.PixelWidth);
        File.Delete(path); // must not be locked by the preview
    }

    [Fact]
    public void TryLoad_CanDecodeAtAReducedWidth()
    {
        using var ws = new TempWorkspace();
        var image = ImageLoader.TryLoad(SavePng(ws, "bg.png", 1920, 1080), 480);
        Assert.Equal(480, image.PixelWidth);
    }

    [Fact]
    public void TryLoad_ReturnsNothingForEmptyMissingOrInvalidFiles()
    {
        using var ws = new TempWorkspace();
        Assert.Null(ImageLoader.TryLoad(null));
        Assert.Null(ImageLoader.TryLoad(""));
        Assert.Null(ImageLoader.TryLoad(Path.Combine(ws.Root, "missing.png")));
        Assert.Null(ImageLoader.TryLoad(ws.Write("broken.png", "not an image")));
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
        var text = ImageLoader.DescribeStretch("The icon", width, height, targetWidth, targetHeight);
        if (expected == "")
            Assert.Equal("", text);
        else
            Assert.Contains(expected, text);
    }
}

/// <summary>The preview is a WPF control, so these tests run on an STA thread.</summary>
public class DashPreviewTests
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

    private static string SavePng(string folder, string name, int width, int height)
    {
        var path = Path.Combine(folder, name);
        using var bitmap = new Bitmap(width, height);
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    private static T Part<T>(DashPreview preview, string name) where T : class => (T)preview.FindName(name);

    [Fact]
    public void SwitchesBetweenThePS4AndPS5Layouts()
    {
        RunSta(() =>
        {
            var preview = new DashPreview();
            Assert.Equal(Visibility.Visible, Part<UIElement>(preview, "PS4Layout").Visibility);
            Assert.Equal(Visibility.Collapsed, Part<UIElement>(preview, "PS5Layout").Visibility);

            preview.IsPS5 = true;

            Assert.Equal(Visibility.Collapsed, Part<UIElement>(preview, "PS4Layout").Visibility);
            Assert.Equal(Visibility.Visible, Part<UIElement>(preview, "PS5Layout").Visibility);
        });
    }

    [Fact]
    public void ShowsTheTitleOrAPlaceholder()
    {
        RunSta(() =>
        {
            var preview = new DashPreview();
            Assert.Equal("Game title", Part<System.Windows.Controls.TextBlock>(preview, "PS4TitleText").Text);

            preview.GameTitle = "Metal Gear Solid";

            Assert.Equal("Metal Gear Solid", Part<System.Windows.Controls.TextBlock>(preview, "PS4TitleText").Text);
            Assert.Equal("Metal Gear Solid", Part<System.Windows.Controls.TextBlock>(preview, "PS5TitleText").Text);
        });
    }

    [Fact]
    public void UsesTheSelectedIconOrFallsBackToTheEmulatorIcon()
    {
        using var ws = new TempWorkspace();
        var fallback = SavePng(ws.Root, "emulator-icon.png", 512, 512);
        var selected = SavePng(ws.Root, "my-icon.png", 256, 256);

        RunSta(() =>
        {
            var preview = new DashPreview();
            Assert.Null(preview.CurrentIcon);
            Assert.Equal(Visibility.Visible, Part<UIElement>(preview, "PS4IconPlaceholder").Visibility);

            preview.FallbackIconPath = fallback;
            Assert.NotNull(preview.CurrentIcon);
            Assert.Equal(Visibility.Collapsed, Part<UIElement>(preview, "PS4IconPlaceholder").Visibility);
            Assert.Equal("", preview.Hints);

            var fallbackIcon = preview.CurrentIcon;
            preview.IconPath = selected;
            Assert.NotNull(preview.CurrentIcon);
            Assert.NotSame(fallbackIcon, preview.CurrentIcon);
            Assert.Contains("upscaled", preview.Hints); // 256x256 icon
        });
    }

    [Fact]
    public void ReportsUnreadableAndDistortedImages()
    {
        using var ws = new TempWorkspace();
        var broken = ws.Write("broken.png", "not an image");
        var wide = SavePng(ws.Root, "wide.png", 1920, 600);

        RunSta(() =>
        {
            var preview = new DashPreview { IconPath = broken, BackgroundPath = wide };
            Assert.Contains("icon could not be read", preview.Hints);
            Assert.Contains("background is 1920×600", preview.Hints);
            Assert.NotNull(preview.CurrentBackground);
        });
    }
}
