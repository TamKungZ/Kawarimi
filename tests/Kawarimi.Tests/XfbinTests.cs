using System.Buffers.Binary;
using System.Text;
using Kawarimi.Formats;

static class XfbinTests
{
    public static byte[] Synthetic(string name, params byte[][] chunkData)
    {
        var header = new byte[40 + 0x40];
        Encoding.ASCII.GetBytes("NUCC").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 0x79);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), 0x40);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(52), (uint)chunkData.Length);
        Encoding.ASCII.GetBytes(name).CopyTo(header, 60);
        var ms = new MemoryStream(); ms.Write(header);
        for (int i = 0; i < chunkData.Length; i++)
        {
            var h = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(h, (uint)chunkData[i].Length);
            BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(4), (uint)i);
            BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(8), 0x79);
            ms.Write(h); ms.Write(chunkData[i]);
        }
        return ms.ToArray();
    }

    public static string? FindGameDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "NSUNS4.exe"))) return d.FullName;
        return null;
    }

    public static void Run()
    {
        var bytes = Synthetic("1nrtbod1", Encoding.ASCII.GetBytes("model 1nrt data"), new byte[0], [1, 2, 3]);
        var f = XfbinFile.Parse(bytes);
        T.Check("xfbin: parses synthetic file", f.Chunks.Count == 3 && f.Version == 0x79);
        T.Check("xfbin: round-trips byte-identical", f.ToBytes().AsSpan().SequenceEqual(bytes));
        T.Check("xfbin: TryMeasure finds exact length inside larger buffer",
            XfbinFile.TryMeasure([.. bytes, .. new byte[500]], out int len, out _) && len == bytes.Length);
        T.Check("xfbin: TryMeasure rejects random data", !XfbinFile.TryMeasure(new byte[200], out _, out _));
        T.Check("xfbin: Parse rejects trailing garbage", T.Throws<InvalidDataException>(() => XfbinFile.Parse([.. bytes, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13])));
        int n = f.ReplaceToken("1nrt", "9k01");
        var after = f.ToBytes();
        T.Check("xfbin: ReplaceToken rewrites header and chunk data", n == 2 && Encoding.ASCII.GetString(after).Contains("9k01bod1") && Encoding.ASCII.GetString(after).Contains("model 9k01 data"));
        T.Check("xfbin: ReplaceToken keeps size and structure", after.Length == bytes.Length && XfbinFile.Parse(after).Chunks.Count == 3);
        T.Check("xfbin: ReplaceToken requires same length", T.Throws<ArgumentException>(() => f.ReplaceToken("9k01", "9k012")));

        {
            const int extra = 5; const uint ms = 0x80;
            var hdr = new byte[28 + ms + 8 * extra];
            Encoding.ASCII.GetBytes("NUCC").CopyTo(hdr, 0);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(4), 0x79);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(16), ms);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(52), 3);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(60), 9);
            BinaryPrimitives.WriteUInt32BigEndian(hdr.AsSpan(64), extra);
            var m = new MemoryStream(); m.Write(hdr);
            for (int i = 0; i < 4; i++)
            {
                var h = new byte[12];
                BinaryPrimitives.WriteUInt32BigEndian(h, 4); BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(4), (uint)(i + 3));
                BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(8), 0x79); m.Write(h); m.Write(new byte[4]);
            }
            var big = m.ToArray();
            var parsed = XfbinFile.Parse(big);
            T.Check("xfbin: large header (second index list) parses and round-trips", parsed.Chunks.Count == 4 && parsed.ToBytes().AsSpan().SequenceEqual(big));
            T.Check("xfbin: TryMeasure handles partial buffers of large files", XfbinFile.TryMeasure(big.AsSpan(0, big.Length - 3), out int plen, out _) && plen == big.Length - 16);
        }

        var game = FindGameDir();
        if (game == null) { Console.WriteLine("SKIP xfbin: real-file tests (not inside the game folder)"); return; }
        var sys = Path.Combine(game, "data", "system");
        int ok = 0, total = 0;
        foreach (var file in Directory.GetFiles(sys, "*.xfbin"))
        {
            var d = File.ReadAllBytes(file);
            if (!XfbinFile.HasMagic(d)) continue;
            total++;
            try { var x = XfbinFile.Parse(d); if (x.ToBytes().AsSpan().SequenceEqual(d)) ok++; else Console.WriteLine("  mismatch " + file); }
            catch (Exception e) { Console.WriteLine($"  {Path.GetFileName(file)}: {e.Message}"); }
        }
        T.Check($"xfbin: all {total} plain game files parse and round-trip", total > 0 && ok == total);
        var ci = Path.Combine(sys, "content_info.xfbin");
        if (File.Exists(ci))
        {
            var x = XfbinFile.Parse(File.ReadAllBytes(ci));
            T.Check("xfbin: reads chunk type/name tables", x.TypeNames.Contains("nuccChunkBinary") && x.ChunkNames.Contains("Page0"));
            T.Check("xfbin: describes chunks", x.Chunks.Any(c => x.Describe(c).StartsWith("nuccChunkBinary")));
        }
    }
}
