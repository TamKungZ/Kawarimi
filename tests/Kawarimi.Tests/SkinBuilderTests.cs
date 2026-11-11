using System.Buffers.Binary;
using System.Text;
using Kawarimi;
using Kawarimi.Formats;
using Kawarimi.Host;

static class SkinBuilderTests
{
    public static byte[] WithNames(string[] types, (int type, string name)[] chunks, string payload)
    {
        static byte[] Strs(IEnumerable<string> l) { var m = new MemoryStream(); foreach (var s in l) { m.Write(Encoding.ASCII.GetBytes(s)); m.WriteByte(0); } return m.ToArray(); }
        var names = chunks.Select(c => c.name).Distinct().ToList();
        byte[] t = Strs(types), n = Strs(names), p = Strs([""]);
        int stringsEnd = 68 + t.Length + p.Length + n.Length, mapStart = (stringsEnd + 3) & ~3;
        int mapBytes = chunks.Length * 12 + chunks.Length * 4;
        int mapSize = mapStart + mapBytes - 40;
        var h = new byte[40 + mapSize];
        Encoding.ASCII.GetBytes("NUCC").CopyTo(h, 0);
        void W(int o, uint v) => BinaryPrimitives.WriteUInt32BigEndian(h.AsSpan(o), v);
        W(4, 0x79); W(16, (uint)mapSize);
        W(28, (uint)types.Length); W(32, (uint)t.Length); W(36, 1); W(40, (uint)p.Length);
        W(44, (uint)names.Count); W(48, (uint)n.Length); W(52, (uint)chunks.Length); W(56, (uint)chunks.Length * 12); W(60, (uint)chunks.Length);
        t.CopyTo(h, 68); p.CopyTo(h, 68 + t.Length); n.CopyTo(h, 68 + t.Length + p.Length);
        for (int i = 0; i < chunks.Length; i++)
        {
            W(mapStart + i * 12, (uint)chunks[i].type);
            W(mapStart + i * 12 + 4, 0);
            W(mapStart + i * 12 + 8, (uint)names.IndexOf(chunks[i].name));
            W(mapStart + chunks.Length * 12 + i * 4, (uint)i);
        }
        var ms = new MemoryStream(); ms.Write(h);
        for (int i = 0; i < chunks.Length; i++)
        {
            var d = Encoding.ASCII.GetBytes(payload); var ch = new byte[12];
            BinaryPrimitives.WriteUInt32BigEndian(ch, (uint)d.Length); BinaryPrimitives.WriteUInt32BigEndian(ch.AsSpan(4), (uint)i);
            BinaryPrimitives.WriteUInt16BigEndian(ch.AsSpan(8), 0x79); ms.Write(ch); ms.Write(d);
        }
        return ms.ToArray();
    }

    public static void Run()
    {
        var sample = XfbinFile.Parse(WithNames(["nuccChunkCoord", "nuccChunkModel"], [(0, "1nrt_root"), (0, "1nrt_head"), (1, "1nrtbod1")], "x"));
        T.Check("builder: reads names/types from chunk map", sample.TypeNames.Count == 2 && sample.ChunkNames.Contains("1nrt_head") && sample.Describe(sample.Chunks[2]) == "nuccChunkModel : 1nrtbod1");

        var root = Path.Combine(Path.GetTempPath(), "kw-sb-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var spc = Path.Combine(root, "cap", "files", "data", "spc");
            Directory.CreateDirectory(spc);
            string[] types = ["nuccChunkCoord", "nuccChunkModel"];
            void Put(string name, string code, params (int, string)[] chunks) =>
                File.WriteAllBytes(Path.Combine(spc, name), WithNames(types, chunks, code + " payload"));
            Put("1nrtbod1.xfbin", "1nrt", (0, "1nrt_root"), (0, "1nrt_head"), (1, "1nrtbod1"));
            Put("1nrtbod1c.xfbin", "1nrt", (1, "1nrtbod1c"));
            Put("1nrtacc1.xfbin", "1nrt", (1, "1nrtacc1"));
            Put("1nrtprm.bin.xfbin", "1nrt", (1, "1nrtprm"));
            Put("2kksbod1.xfbin", "2kks", (0, "2kks_root"), (0, "2kks_head"), (1, "2kksbod1"));
            Put("2kksprm.bin.xfbin", "2kks", (1, "2kksprm"));
            Put("2kksskl1.xfbin", "2kks", (1, "2kksskl1"));
            Put("2kks2guyprm.bin.xfbin", "2kks", (1, "partner"));
            File.WriteAllText(Path.Combine(spc, "readme.txt"), "ignored");

            var mod = Path.Combine(root, "mods", "KakashiAsNaruto");
            var r = SkinBuilder.Compose(new SkinBuilder.ComposeOptions(Path.Combine(root, "cap"), "2kks", "1nrt", "9k01", "2kks", "Kakashi as Naruto", mod));
            var files = Directory.GetFiles(Path.Combine(mod, "files")).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            T.Check("compose: model parts from model character, rest from gameplay character",
                files.SequenceEqual(["9k01acc1.xfbin", "9k01bod1.xfbin", "9k01bod1c.xfbin", "9k01prm.bin.xfbin", "9k01skl1.xfbin"]));
            T.Check("compose: partner-combo files of another code are skipped", !files.Any(f => f!.Contains("guy")));
            T.Check("compose: no warnings for a complete set", r.Warnings.Count == 0);
            var bod = XfbinFile.Parse(File.ReadAllBytes(Path.Combine(mod, "files", "9k01bod1.xfbin")));
            T.Check("compose: model re-coded to the new skin code (names and data)", bod.ChunkNames.Contains("9k01_root") && !bod.ChunkNames.Any(n => n.Contains("1nrt")) && Encoding.ASCII.GetString(bod.Chunks[0].Data).StartsWith("9k01"));
            var prm = XfbinFile.Parse(File.ReadAllBytes(Path.Combine(mod, "files", "9k01prm.bin.xfbin")));
            T.Check("compose: gameplay file comes from the gameplay character", Encoding.ASCII.GetString(prm.Chunks[0].Data).StartsWith("9k01") && prm.ChunkNames.Contains("9k01prm"));

            var reg = new SkinRegistry(); var loader = new ModLoader(Path.Combine(root), reg, _ => { });
            loader.LoadAll();
            T.Check("compose: produced mod loads in Kawarimi", reg.All.Count == 1 && reg.All[0].SkinCode == "9k01" && loader.Report[0].Status == ModStatus.Loaded && loader.Report[0].Messages.Count == 0);

            var r2 = SkinBuilder.Compose(new SkinBuilder.ComposeOptions(Path.Combine(root, "cap"), "2kks", "3zzz", "9k02", "2kks", "x", Path.Combine(root, "m2")));
            T.Check("compose: warns when model character was not captured", r2.Warnings.Any(w => w.Contains("3zzz")));
            T.Check("compose: rejects different code lengths", T.Throws<ArgumentException>(() => SkinBuilder.Compose(new SkinBuilder.ComposeOptions(Path.Combine(root, "cap"), "2kks", "1nrt", "9k0", "2kks", "x", Path.Combine(root, "m3")))));

            var cmp = ModelComparer.Compare(Path.Combine(spc, "1nrtbod1.xfbin"), "1nrt", Path.Combine(spc, "2kksbod1.xfbin"), "2kks");
            T.Check("compare: same bone names after normalising codes", cmp.SameNames && cmp.Shared == 3);
            Put("3diffbod1.xfbin", "3dif", (0, "3dif_root"), (0, "3dif_tail"), (1, "3difbod1"));
            var cmp2 = ModelComparer.Compare(Path.Combine(spc, "1nrtbod1.xfbin"), "1nrt", Path.Combine(spc, "3diffbod1.xfbin"), "3dif");
            T.Check("compare: reports bones missing on either side",
                !cmp2.SameNames && cmp2.OnlyInA.Contains("nuccChunkCoord:{code}_head") && cmp2.OnlyInB.Contains("nuccChunkCoord:{code}_tail"));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
