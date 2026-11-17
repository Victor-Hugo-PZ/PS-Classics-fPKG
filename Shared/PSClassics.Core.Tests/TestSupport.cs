using System.Diagnostics;
using PS_Classics_fPKG_Builder;
using Xunit;

// AppPaths is process-wide state, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PSClassics.Tests;

/// <summary>Locations and capabilities used by the tests (Windows, Linux and macOS).</summary>
public static class TestEnvironment
{
    /// <summary>Repository root (the folder that contains .gitmodules).</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>The git-ignored Tools folder with the third-party tools (not part of the repository).</summary>
    public static string ToolsDirectory => Path.Combine(RepoRoot, "Tools");

    /// <summary>The Tools folder exists (game databases, emulators, Windows tools).</summary>
    public static bool ToolsAvailable =>
        File.Exists(Path.Combine(ToolsDirectory, "PS4", "gengp4_patch.exe")) &&
        Directory.Exists(Path.Combine(ToolsDirectory, "PS4", "emus"));

    /// <summary>The Windows tools can run: on Windows directly, on Linux/macOS through a ready Wine setup.</summary>
    public static bool ToolsRunnable => ToolsAvailable && (Platform.IsWindows || Wine.GetStatus().IsReady);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".gitmodules")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

/// <summary>A [Fact] that needs the Tools folder (data only: game databases, emulator files). Skipped without it.</summary>
public sealed class RequiresToolsFactAttribute : FactAttribute
{
    public RequiresToolsFactAttribute()
    {
        if (!TestEnvironment.ToolsAvailable)
            Skip = "The Tools folder is not available (see README, 'Tools folder').";
    }
}

/// <summary>A [Fact] that runs the Windows tools (directly on Windows, through Wine on Linux/macOS). Skipped when they cannot run.</summary>
public sealed class RequiresToolRunnerFactAttribute : FactAttribute
{
    public RequiresToolRunnerFactAttribute()
    {
        if (!TestEnvironment.ToolsAvailable)
            Skip = "The Tools folder is not available (see README, 'Tools folder').";
        else if (!TestEnvironment.ToolsRunnable)
            Skip = "Wine is not set up (Linux/macOS): the Windows tools cannot run.";
    }
}

/// <summary>A [Fact] that compares with the original Windows tools: only on Windows with the Tools folder.</summary>
public sealed class WindowsToolsFactAttribute : FactAttribute
{
    public WindowsToolsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Compares with the original Windows tools: Windows only.";
        else if (!TestEnvironment.ToolsAvailable)
            Skip = "The Tools folder is not available (see README, 'Tools folder').";
    }
}

/// <summary>
/// A temporary working folder. AppPaths.BaseDirectory and AppPaths.CacheDirectory point into it while the instance is alive,
/// so Cache and Tools are resolved inside it and the repository is never touched.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    private readonly string _previousBaseDirectory;

    public string Root { get; }
    public string Cache => Path.Combine(Root, "Cache");
    public string Tools => Path.Combine(Root, "Tools");

    public TempWorkspace(bool linkTools = false)
    {
        Root = Path.Combine(Path.GetTempPath(), "psfpkg-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        _previousBaseDirectory = AppPaths.BaseDirectory;
        AppPaths.BaseDirectory = Root;
        AppPaths.CacheDirectory = Cache;

        if (linkTools)
            LinkTools();
    }

    /// <summary>Makes the real Tools folder visible as Tools inside the workspace without copying it.</summary>
    private void LinkTools()
    {
        if (OperatingSystem.IsWindows())
        {
            // A directory junction needs no admin rights (a symbolic link would)
            var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Tools}\" \"{TestEnvironment.ToolsDirectory}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi);
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
        }
        else
        {
            Directory.CreateSymbolicLink(Tools, TestEnvironment.ToolsDirectory);
        }

        if (!Directory.Exists(Tools))
            throw new InvalidOperationException("Could not link the Tools folder.");
    }

    /// <summary>Path inside the workspace. Backslashes are accepted as separators on every OS.</summary>
    public string PathOf(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('\\', Path.DirectorySeparatorChar));

    public string Write(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
        return path;
    }

    public string Write(string relativePath, byte[] content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        AppPaths.BaseDirectory = _previousBaseDirectory;
        AppPaths.CacheDirectory = null;
        try
        {
            // Remove the link first so the real Tools folder is never deleted through it.
            if (Directory.Exists(Tools) && new DirectoryInfo(Tools).LinkTarget != null)
                Directory.Delete(Tools, false);
            Directory.Delete(Root, true);
        }
        catch (IOException) { /* best effort cleanup of temp files */ }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Creates small test images on every OS (SkiaSharp encodes PNG, JPEG and WebP; BMP is written by hand).</summary>
public static class TestImages
{
    public static string Save(string path, int width, int height, string format = "png", byte r = 20, byte g = 40, byte b = 160, byte a = 255)
    {
        using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
        bitmap.Erase(new SkiaSharp.SKColor(r, g, b, a));
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        if (format == "bmp")
        {
            File.WriteAllBytes(path, Bmp24(width, height, r, g, b));
            return path;
        }

        var encoded = format switch
        {
            "jpg" or "jpeg" => SkiaSharp.SKEncodedImageFormat.Jpeg,
            "webp" => SkiaSharp.SKEncodedImageFormat.Webp,
            _ => SkiaSharp.SKEncodedImageFormat.Png,
        };
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(encoded, 95);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>A plain 24 bit BMP (bottom-up rows, padded to 4 bytes).</summary>
    private static byte[] Bmp24(int width, int height, byte r, byte g, byte b)
    {
        int rowSize = (width * 3 + 3) / 4 * 4;
        int imageSize = rowSize * height;
        var bytes = new byte[54 + imageSize];
        bytes[0] = (byte)'B'; bytes[1] = (byte)'M';
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        BitConverter.GetBytes(imageSize).CopyTo(bytes, 34);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int o = 54 + y * rowSize + x * 3;
                bytes[o] = b; bytes[o + 1] = g; bytes[o + 2] = r;
            }
        return bytes;
    }
}
