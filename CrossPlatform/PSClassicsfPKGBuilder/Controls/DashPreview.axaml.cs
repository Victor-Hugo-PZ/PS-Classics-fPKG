using Avalonia.Controls;
using Avalonia.Media.Imaging;
using PS_Classics_fPKG_Builder;

namespace PSClassicsfPKGBuilder.Controls;

/// <summary>
/// Live preview of how the game tile looks on the PS4 / PS5 home screen (an approximation, like in the Windows app).
/// Call <see cref="Update"/> whenever the icon, background, title or console changes.
/// </summary>
public partial class DashPreview : UserControl
{
    public DashPreview() => InitializeComponent();

    /// <summary>The icon currently shown (selected image or emulator default), for thumbnails next to the preview.</summary>
    public Bitmap CurrentIcon { get; private set; }

    /// <summary>The background currently shown (selected image or emulator default).</summary>
    public Bitmap CurrentBackground { get; private set; }

    /// <summary>Updates the preview and returns the hints about the selected images (same text as the Windows app).</summary>
    public IReadOnlyList<string> Update(string iconPath, string backgroundPath, string fallbackIconPath, string fallbackBackgroundPath, string title, bool isPS5)
    {
        var icon = Load(iconPath, 512) ?? (string.IsNullOrWhiteSpace(iconPath) ? Load(fallbackIconPath, 512) : null);
        var background = Load(backgroundPath, 1280) ?? (string.IsNullOrWhiteSpace(backgroundPath) ? Load(fallbackBackgroundPath, 1280) : null);

        CurrentIcon = icon;
        CurrentBackground = background;
        BackgroundImage.Source = background;
        PS4IconImage.Source = icon;
        PS5IconImage.Source = icon;
        PS4IconPlaceholder.IsVisible = icon == null;
        PS5IconPlaceholder.IsVisible = icon == null;

        var text = string.IsNullOrWhiteSpace(title) ? "Game title" : title;
        PS4TitleText.Text = text;
        PS5TitleText.Text = text;

        PS4Layout.IsVisible = !isPS5;
        PS5Layout.IsVisible = isPS5;

        return ImageConverter.PreviewHints(iconPath, backgroundPath);
    }

    /// <summary>
    /// Loads an image for display without locking the file. Formats the toolkit cannot decode are converted by the
    /// shared Core library, like the build does. Returns null when the path is empty or not an image.
    /// </summary>
    public static Bitmap Load(string path, int width)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            return Bitmap.DecodeToWidth(stream, width);
        }
        catch (Exception)
        {
            var png = ImageConverter.ToPreviewPng(path, width);
            if (png == null) return null;
            try
            {
                using var stream = new MemoryStream(png);
                return new Bitmap(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
