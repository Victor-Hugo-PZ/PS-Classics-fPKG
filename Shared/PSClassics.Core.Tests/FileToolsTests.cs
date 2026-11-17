using DiscUtils.Iso9660;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class PspToolsTests
{
    private static byte[] RandomBytes(int length, int seed = 1)
    {
        var data = new byte[length];
        new Random(seed).NextBytes(data);
        return data;
    }

    [Fact]
    public void FindOffset_FindsTheQueryAtTheStartMiddleAndEnd()
    {
        using var ws = new TempWorkspace();
        var data = RandomBytes(100_000);
        var query = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        foreach (var offset in new[] { 0, 50_000, 100_000 - query.Length })
        {
            Array.Copy(query, 0, data, offset, query.Length);
            var file = ws.Write("data.bin", data);
            Assert.Equal(offset, PspTools.FindOffset(file, query));
            Array.Copy(RandomBytes(query.Length, 99), 0, data, offset, query.Length); // restore noise
        }
    }

    [Fact]
    public void FindOffset_FindsAMatchThatSpansTwoReadChunks()
    {
        using var ws = new TempWorkspace();
        const int chunk = 4 * 1024 * 1024;
        var data = RandomBytes(chunk * 2 + 1234);
        var query = Enumerable.Range(0, 64).Select(i => (byte)(i * 3 + 1)).ToArray();
        long offset = chunk - 17; // crosses the boundary of the first chunk
        Array.Copy(query, 0, data, offset, query.Length);

        Assert.Equal(offset, PspTools.FindOffset(ws.Write("big.bin", data), query));
    }

    [Fact]
    public void FindOffset_FindsALargeQueryLikeARealEboot()
    {
        using var ws = new TempWorkspace();
        var data = RandomBytes(9 * 1024 * 1024);
        var query = RandomBytes(512_320, 7);
        const int offset = 5 * 1024 * 1024 + 3;
        Array.Copy(query, 0, data, offset, query.Length);

        Assert.Equal(offset, PspTools.FindOffset(ws.Write("image.bin", data), query));
    }

    [Fact]
    public void FindOffset_IgnoresPartialMatchesAndReturnsTheFirstOccurrence()
    {
        using var ws = new TempWorkspace();
        var data = new byte[] { 9, 0xAA, 0xAA, 0xAA, 0xAA, 0xAB, 1, 0xAA, 0xAA, 0xAB };
        var file = ws.Write("pattern.bin", data);

        Assert.Equal(3, PspTools.FindOffset(file, new byte[] { 0xAA, 0xAA, 0xAB }));
    }

    [Fact]
    public void FindOffset_ReturnsMinusOneWhenThereIsNoMatch()
    {
        using var ws = new TempWorkspace();
        var file = ws.Write("data.bin", RandomBytes(5000));

        Assert.Equal(-1, PspTools.FindOffset(file, Enumerable.Repeat((byte)0xEE, 40).ToArray()));
        Assert.Equal(-1, PspTools.FindOffset(file, new byte[10_000]));  // query longer than the file
        Assert.Equal(-1, PspTools.FindOffset(file, Array.Empty<byte>())); // empty query
    }

    [Fact]
    public void ReadBytes_ReadsAtAnOffsetAndZeroPadsAfterTheEndOfTheFile()
    {
        using var ws = new TempWorkspace();
        var file = ws.Write("data.bin", new byte[] { 1, 2, 3, 4, 5 });

        Assert.Equal(new byte[] { 2, 3 }, PspTools.ReadBytes(file, 1, 2));
        Assert.Equal(new byte[] { 4, 5, 0, 0 }, PspTools.ReadBytes(file, 3, 4));
        Assert.Equal(new byte[] { 0, 0 }, PspTools.ReadBytes(file, 50, 2));
    }

    [Fact]
    public void WriteBytes_OverwritesOnlyTheGivenRange()
    {
        using var ws = new TempWorkspace();
        var file = ws.Write("data.bin", new byte[] { 1, 2, 3, 4, 5, 6 });

        PspTools.WriteBytes(file, 2, new byte[] { 0xA, 0xB });

        Assert.Equal(new byte[] { 1, 2, 0xA, 0xB, 5, 6 }, File.ReadAllBytes(file));
    }

    [Fact]
    public void ReplaceEbootInImage_ReplacesTheEncryptedEbootWithTheDecryptedOne()
    {
        using var ws = new TempWorkspace();
        var image = RandomBytes(2 * 1024 * 1024);
        var encrypted = RandomBytes(700_000, 5);
        var decrypted = RandomBytes(700_000, 6);
        const int offset = 100_000;
        Array.Copy(encrypted, 0, image, offset, encrypted.Length);

        var imagePath = ws.Write("USER_L0.IMG", image);
        var encryptedPath = ws.Write("temp_eboot.bin", encrypted);
        var decryptedPath = ws.Write("temp_eboot.bin.dec", decrypted);

        Assert.True(PspTools.ReplaceEbootInImage(imagePath, encryptedPath, decryptedPath));

        var result = File.ReadAllBytes(imagePath);
        Assert.Equal(image.Length, result.Length);
        Assert.Equal(decrypted, result.Skip(offset).Take(decrypted.Length).ToArray());
        Assert.Equal(image.Take(offset), result.Take(offset));
        Assert.Equal(image.Skip(offset + decrypted.Length), result.Skip(offset + decrypted.Length));
    }

    [Fact]
    public void ReplaceEbootInImage_LeavesTheImageUntouchedWhenTheEbootIsNotInside()
    {
        using var ws = new TempWorkspace();
        var image = RandomBytes(300_000);
        var imagePath = ws.Write("USER_L0.IMG", image);
        var encryptedPath = ws.Write("temp_eboot.bin", RandomBytes(20_000, 5));
        var decryptedPath = ws.Write("temp_eboot.bin.dec", RandomBytes(20_000, 6));

        Assert.False(PspTools.ReplaceEbootInImage(imagePath, encryptedPath, decryptedPath));
        Assert.Equal(image, File.ReadAllBytes(imagePath));
    }
}

public class IsoToolsTests
{
    private static string BuildIso(TempWorkspace ws, string fileName, params (string Path, byte[] Data)[] files)
    {
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "TEST" };
        foreach (var (path, data) in files)
            builder.AddFile(path, data);
        var isoPath = Path.Combine(ws.Root, fileName);
        builder.Build(isoPath);
        return isoPath;
    }

    private static byte[] Bytes(string text) => System.Text.Encoding.ASCII.GetBytes(text);

    [Fact]
    public void FileExistInISO_ChecksFilesInsideTheImage()
    {
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "psp.iso", (@"PSP_GAME\PARAM.SFO", Bytes("sfo")), ("UMD_DATA.BIN", Bytes("ULUS-10041|1")));

        Assert.True(IsoTools.FileExistInISO(iso, @"\PSP_GAME\PARAM.SFO"));
        Assert.True(IsoTools.FileExistInISO(iso, "UMD_DATA.BIN"));
        Assert.False(IsoTools.FileExistInISO(iso, @"\PSP_GAME\SYSDIR\EBOOT.BIN"));
        Assert.False(IsoTools.FileExistInISO(Path.Combine(ws.Root, "missing.iso"), "UMD_DATA.BIN"));
    }

    [Fact]
    public void ExtractFileFromPSPISO_ExtractsTheFileAndCreatesTheDestinationFolder()
    {
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "psp.iso", (@"PSP_GAME\SYSDIR\EBOOT.BIN", Bytes("encrypted eboot")));
        var destination = Path.Combine(ws.Root, "new folder", "temp_eboot.bin");

        var result = IsoTools.ExtractFileFromPSPISO(iso, @"\PSP_GAME\SYSDIR\EBOOT.BIN", destination);

        Assert.Equal(destination, result);
        Assert.Equal("encrypted eboot", File.ReadAllText(destination));
    }

    [Fact]
    public void ExtractFileFromPSPISO_ReturnsEmptyAndCreatesNothingWhenTheFileIsMissing()
    {
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "psp.iso", ("OTHER.TXT", Bytes("x")));
        var destination = Path.Combine(ws.Root, "out.bin");

        Assert.Equal("", IsoTools.ExtractFileFromPSPISO(iso, "UMD_DATA.BIN", destination));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void GetELFfromISO_ExtractsTheBootElfIntoTheCacheFolder()
    {
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "game.iso", ("SLUS_212.58", Bytes("ELF-CONTENT")));

        var path = IsoTools.GetELFfromISO(iso, "SLUS_212.58");

        Assert.Equal(Path.Combine(ws.Cache, "SLUS_212.58"), path);
        Assert.Equal("ELF-CONTENT", File.ReadAllText(path));
    }

    [Fact]
    public void GetELFfromISO_ReturnsEmptyAndLeavesNoEmptyFileWhenTheElfIsMissing()
    {
        // Regression: the original created an empty destination file before it noticed the ELF did not exist.
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "game.iso", ("OTHER.TXT", Bytes("x")));

        Assert.Equal("", IsoTools.GetELFfromISO(iso, "SLUS_212.58"));
        Assert.False(File.Exists(Path.Combine(ws.Cache, "SLUS_212.58")));
        Assert.Equal("", IsoTools.GetELFfromISO(iso, ""));
    }

    [Fact]
    public void ExtractedFilesAreNotLockedAfterwards()
    {
        // Regression: the original left the extracted file open.
        using var ws = new TempWorkspace();
        var iso = BuildIso(ws, "game.iso", ("SLUS_212.58", Bytes("ELF")));

        var path = IsoTools.GetELFfromISO(iso, "SLUS_212.58");

        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void IsConfigAvailable_ReturnsFalseForMissingDatabasesWithoutShowingAMessageBox()
    {
        Assert.False(IsoTools.IsConfigAvailable("SLUS_212.58", @"C:\does\not\exist.dat"));
    }

    [RequiresToolsFact]
    public void RealConfigDatabases_CanBeReadAndQueried()
    {
        foreach (var name in new[] { "configs_txt.dat", "configs_lua.dat", "configs_ps3.dat", "widescreen.dat" })
        {
            var dat = Path.Combine(TestEnvironment.ToolsDirectory, "PS4", "ps2-configs", name);

            string first;
            using (var stream = File.OpenRead(dat))
            {
                var reader = new DiscUtils.Udf.UdfReader(stream, 2048);
                first = Path.GetFileName(reader.GetFiles(@"\").First());
            }

            Assert.True(IsoTools.IsConfigAvailable(first, dat), $"{first} should be listed in {name}");
            Assert.False(IsoTools.IsConfigAvailable("NOT_IN_THE_DATABASE", dat));
            Assert.NotEqual("", IsoTools.GetPNACHFromDAT(dat, first));
        }
    }
}

public class FileUtilsAndPathsTests
{
    [Fact]
    public void CopyDirectory_CopiesFilesNestedFoldersAndEmptyFolders()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"src\a.txt", "a");
        ws.Write(@"src\sub\deep\b.txt", "b");
        Directory.CreateDirectory(Path.Combine(ws.Root, "src", "empty"));

        FileUtils.CopyDirectory(Path.Combine(ws.Root, "src"), Path.Combine(ws.Root, "dst"), true);

        Assert.Equal("a", File.ReadAllText(Path.Combine(ws.Root, "dst", "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(ws.Root, "dst", "sub", "deep", "b.txt")));
        Assert.True(Directory.Exists(Path.Combine(ws.Root, "dst", "empty")));
    }

    [Fact]
    public void CopyDirectory_HonoursTheOverwriteFlag()
    {
        using var ws = new TempWorkspace();
        ws.Write(@"src\a.txt", "new");
        ws.Write(@"dst\a.txt", "old");
        var src = Path.Combine(ws.Root, "src");
        var dst = Path.Combine(ws.Root, "dst");

        Assert.Throws<IOException>(() => FileUtils.CopyDirectory(src, dst, false));
        Assert.Equal("old", File.ReadAllText(Path.Combine(dst, "a.txt")));

        FileUtils.CopyDirectory(src, dst, true);
        Assert.Equal("new", File.ReadAllText(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public void CopyDirectory_ThrowsForAMissingSource()
    {
        using var ws = new TempWorkspace();
        Assert.Throws<DirectoryNotFoundException>(() => FileUtils.CopyDirectory(Path.Combine(ws.Root, "nope"), Path.Combine(ws.Root, "dst"), true));
    }

    [Fact]
    public void AppPaths_ResolvesToolsAndCacheFromTheBaseDirectoryWithoutTrailingSeparator()
    {
        var previous = AppPaths.BaseDirectory;
        var app = Path.Combine(Path.GetTempPath(), "Some App");
        try
        {
            AppPaths.BaseDirectory = app + Path.DirectorySeparatorChar;
            Assert.Equal(app, AppPaths.BaseDirectory);
            Assert.Equal(Path.Combine(app, "Tools"), AppPaths.ToolsDirectory);

            // Next to the program, except on macOS where the app bundle must not be written to
            var expectedCache = OperatingSystem.IsMacOS() ? Path.Combine(AppPaths.DataDirectory, "Cache") : Path.Combine(app, "Cache");
            Assert.Equal(expectedCache, AppPaths.CacheDirectory);

            AppPaths.CacheDirectory = Path.Combine(app, "MyCache");
            Assert.Equal(Path.Combine(app, "MyCache"), AppPaths.CacheDirectory);
        }
        finally
        {
            AppPaths.BaseDirectory = previous;
            AppPaths.CacheDirectory = null;
        }
    }

    [Fact]
    public void AppPaths_ToolsAvailable_ChecksForThePublishingToolsAndEmulators()
    {
        using var ws = new TempWorkspace();
        Assert.False(AppPaths.ToolsAvailable());

        ws.Write(@"Tools\PS4\gengp4_patch.exe", new byte[] { 1 });
        Assert.False(AppPaths.ToolsAvailable()); // the emulators folder is still missing

        Directory.CreateDirectory(Path.Combine(ws.Root, "Tools", "PS4", "emus"));
        Assert.True(AppPaths.ToolsAvailable());
    }

    [Fact]
    public void AppPaths_DefaultsToTheFolderOfTheExecutableNotTheWorkingDirectory()
    {
        var oldWorkingDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            Assert.Equal(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), AppPaths.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        }
        finally { Environment.CurrentDirectory = oldWorkingDirectory; }
    }
}
