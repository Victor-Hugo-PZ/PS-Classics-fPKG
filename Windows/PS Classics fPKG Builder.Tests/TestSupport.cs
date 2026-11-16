using System.Diagnostics;
using PS_Classics_fPKG_Builder;
using Xunit;

// AppPaths is process-wide state, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PSClassics.Tests;

/// <summary>Locations used by the tests.</summary>
public static class TestEnvironment
{
    /// <summary>Repository root (the folder that contains .gitmodules).</summary>
    public static string RepoRoot { get; } = FindRepoRoot();

    /// <summary>The git-ignored Tools folder with the third-party tools (not part of the repository).</summary>
    public static string ToolsDirectory => Path.Combine(RepoRoot, "Tools");

    public static bool ToolsAvailable =>
        File.Exists(Path.Combine(ToolsDirectory, "7z.exe")) &&
        File.Exists(Path.Combine(ToolsDirectory, "PS4", "gengp4_patch.exe"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".gitmodules")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

/// <summary>A [Fact] that is skipped (and reported as skipped) when the Tools folder is not available.</summary>
public sealed class RequiresToolsFactAttribute : FactAttribute
{
    public RequiresToolsFactAttribute()
    {
        if (!TestEnvironment.ToolsAvailable)
            Skip = "The Tools folder is not available (see README, 'Tools folder').";
    }
}

/// <summary>
/// A temporary working folder. AppPaths.BaseDirectory points to it while the instance is alive,
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

        if (linkTools)
            LinkTools();
    }

    /// <summary>Makes the real Tools folder visible as Tools inside the workspace (directory junction, no copy, no admin rights needed).</summary>
    private void LinkTools()
    {
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
        if (!Directory.Exists(Tools))
            throw new InvalidOperationException("Could not create the Tools junction.");
    }

    public string Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content);
        return path;
    }

    public string Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        AppPaths.BaseDirectory = _previousBaseDirectory;
        try
        {
            // Remove the junction first so the real Tools folder is never deleted through it.
            if (Directory.Exists(Tools) && new DirectoryInfo(Tools).LinkTarget != null)
                Directory.Delete(Tools, false);
            Directory.Delete(Root, true);
        }
        catch (IOException) { /* best effort cleanup of temp files */ }
        catch (UnauthorizedAccessException) { }
    }
}
