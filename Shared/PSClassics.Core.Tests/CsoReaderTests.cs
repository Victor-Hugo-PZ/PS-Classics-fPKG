using System.IO.Compression;
using PS_Classics_fPKG_Builder;
using Xunit;

namespace PSClassics.Tests;

public class CsoReaderTests
{
    /// <summary>Writes a CSO file like the PSP tools do: raw deflate blocks, or the block as it is when deflate does not make it smaller.</summary>
    private static void WriteCso(string path, byte[] iso, int blockSize = 2048, int shift = 0)
    {
        var blockCount = (iso.Length + blockSize - 1) / blockSize;
        var blocks = new List<(byte[] Data, bool Plain)>();
        for (var i = 0; i < blockCount; i++)
        {
            var length = Math.Min(blockSize, iso.Length - i * blockSize);
            var block = iso.AsSpan(i * blockSize, length).ToArray();
            using var memory = new MemoryStream();
            using (var deflate = new DeflateStream(memory, CompressionLevel.Optimal, true)) deflate.Write(block);
            var packed = memory.ToArray();
            blocks.Add(packed.Length < block.Length ? (packed, false) : (block, true));
        }

        using var output = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(output);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("CISO"));
        writer.Write(24u);
        writer.Write((ulong)iso.Length);
        writer.Write((uint)blockSize);
        writer.Write((byte)1);
        writer.Write((byte)shift);
        writer.Write((ushort)0);

        long position = 24 + (blockCount + 1) * 4;
        var align = (1 << shift) - 1;
        var offsets = new List<long>();
        foreach (var (data, _) in blocks)
        {
            position = (position + align) & ~(long)align; // blocks start at multiples of the index unit
            offsets.Add(position);
            position += data.Length;
        }
        offsets.Add((position + align) & ~(long)align);
        for (var i = 0; i < blockCount; i++)
            writer.Write((uint)(offsets[i] >> shift) | (blocks[i].Plain ? 0x80000000u : 0));
        writer.Write((uint)(offsets[blockCount] >> shift));
        for (var i = 0; i < blockCount; i++)
        {
            while (output.Position < offsets[i]) output.WriteByte(0);
            output.Write(blocks[i].Data);
        }
    }

    private static byte[] SampleIso(int length)
    {
        // Half repeating text (compresses) and half noise (does not), so both kinds of blocks are used
        var data = new byte[length];
        var text = System.Text.Encoding.ASCII.GetBytes("PSP_GAME PARAM.SFO ");
        for (var i = 0; i < length / 2; i++) data[i] = text[i % text.Length];
        new Random(5).NextBytes(data.AsSpan(length / 2));
        return data;
    }

    [Theory]
    [InlineData(2048 * 10, 2048, 0)]
    [InlineData(2048 * 10 + 700, 2048, 0)]   // the last block is shorter
    [InlineData(2048 * 10 + 1, 2048, 2)]     // index shift, like PSP tools use
    [InlineData(4096 * 6, 4096, 0)]
    public void ConvertToIso_GivesBackTheOriginalBytes(int length, int blockSize, int shift)
    {
        using var ws = new TempWorkspace();
        var iso = SampleIso(length);
        var cso = Path.Combine(ws.Root, "game.cso");
        WriteCso(cso, iso, blockSize, shift);

        var output = Path.Combine(ws.Root, "game.iso");
        CsoReader.ConvertToIso(cso, output);

        Assert.Equal(iso, File.ReadAllBytes(output));
    }

    [Fact]
    public void ConvertToIso_ReportsProgressUpTo100()
    {
        using var ws = new TempWorkspace();
        var cso = Path.Combine(ws.Root, "game.cso");
        WriteCso(cso, SampleIso(2048 * 50));
        var seen = new List<int>();

        CsoReader.ConvertToIso(cso, Path.Combine(ws.Root, "game.iso"), seen.Add);

        Assert.Equal(100, seen.Last());
        Assert.Equal(seen.OrderBy(x => x), seen);
    }

    [Fact]
    public void IsCso_RecognizesTheFormats()
    {
        using var ws = new TempWorkspace();
        var cso = Path.Combine(ws.Root, "a.cso");
        WriteCso(cso, SampleIso(4096));
        var ziso = ws.Write("z.cso", "ZISO" + new string('x', 40));

        Assert.True(CsoReader.IsCso(cso));
        Assert.False(CsoReader.IsZiso(cso));
        Assert.True(CsoReader.IsZiso(ziso));
        Assert.False(CsoReader.IsCso(ws.Write("plain.iso", new byte[100])));
        Assert.False(CsoReader.IsCso(Path.Combine(ws.Root, "missing.cso")));
    }

    [Fact]
    public void ConvertToIso_RejectsAFileThatIsNotACso()
    {
        using var ws = new TempWorkspace();
        var fake = ws.Write("fake.cso", new byte[200]);

        Assert.Throws<InvalidDataException>(() => CsoReader.ConvertToIso(fake, Path.Combine(ws.Root, "x.iso")));
    }

    [Fact]
    public void ConvertToIso_RejectsACutShortFile()
    {
        using var ws = new TempWorkspace();
        var cso = Path.Combine(ws.Root, "game.cso");
        WriteCso(cso, SampleIso(2048 * 20));
        var bytes = File.ReadAllBytes(cso);
        File.WriteAllBytes(cso, bytes.AsSpan(0, bytes.Length / 2).ToArray());

        Assert.Throws<InvalidDataException>(() => CsoReader.ConvertToIso(cso, Path.Combine(ws.Root, "x.iso")));
    }

    [Fact]
    public void ConvertToCache_MakesTheIsoOnceAndReusesIt()
    {
        using var ws = new TempWorkspace();
        var iso = SampleIso(2048 * 8);
        var cso = Path.Combine(ws.Root, "My Game.cso");
        WriteCso(cso, iso);

        var first = CsoReader.ConvertToCache(cso);
        var stamp = File.GetLastWriteTimeUtc(first);
        System.Threading.Thread.Sleep(30);
        var second = CsoReader.ConvertToCache(cso);

        Assert.Equal(first, second);
        Assert.EndsWith("My Game.iso", first);
        Assert.Equal(iso, File.ReadAllBytes(first));
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(second)); // not written again
        Assert.False(File.Exists(first + ".part"));
    }

    [Fact]
    public void ConvertToCache_RefusesZiso()
    {
        using var ws = new TempWorkspace();
        var ziso = ws.Write("z.cso", "ZISO" + new string('x', 40));

        Assert.Throws<NotSupportedException>(() => CsoReader.ConvertToCache(ziso));
    }

    [Fact]
    public void ConvertedPspIsoIsReadable()
    {
        // A real PSP ISO made with DiscUtils, compressed and converted back: the PARAM.SFO is still found
        using var ws = new TempWorkspace();
        var builder = new DiscUtils.Iso9660.CDBuilder { UseJoliet = true, VolumeIdentifier = "PSP" };
        builder.AddFile(@"PSP_GAME\PARAM.SFO", SfoBuilder.Build("UP9000-ULUS10041_00-ULUS10041PSPFPKG", "Lumines", "ULUS10041"));
        var iso = Path.Combine(ws.Root, "psp.iso");
        builder.Build(iso);
        var cso = Path.Combine(ws.Root, "psp.cso");
        WriteCso(cso, File.ReadAllBytes(iso));

        var converted = CsoReader.ConvertToCache(cso);

        Assert.True(IsoTools.FileExistInISO(converted, @"\PSP_GAME\PARAM.SFO"));
    }
}

public class CsoDropTests
{
    [Fact]
    public void Classify_ACsoIsAPspDisc()
    {
        using var ws = new TempWorkspace();
        var cso = ws.Write("game.cso", "CISO" + new string('x', 40));
        var other = ws.Write("other.cso", "nothing useful");

        Assert.Equal(DroppedFileKind.PSPDisc, DroppedFiles.Classify(cso));
        Assert.Equal(DroppedFileKind.Unknown, DroppedFiles.Classify(other));
    }
}
