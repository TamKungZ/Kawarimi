using System.Buffers.Binary;
using System.Text;
using Kawarimi;
using Kawarimi.Formats;

static class FormatsTests
{
    public static void Run()
    {
        Cipher();
        Png();
        Texture();
        Catalog();
        RealModel();
    }

    static void Cipher()
    {
        var header = Encoding.ASCII.GetBytes("CRILAYLA");
        StormCipher.Apply(header);
        T.Check("cipher: CRILAYLA encrypts to the archive signature", header.SequenceEqual(StormCipher.EncryptedSignature));
        var data = Enumerable.Range(0, 1001).Select(i => (byte)i).ToArray();
        var copy = (byte[])data.Clone();
        StormCipher.Apply(copy);
        StormCipher.Apply(copy);
        T.Check("cipher: symmetric for odd lengths", copy.SequenceEqual(data));
        T.Check("cipher: Encode refuses non-xfbin data", T.Throws<InvalidDataException>(() => StormCipher.Encode(new byte[16])));
    }

    static void Png()
    {
        var rgb = Enumerable.Range(0, 7 * 5 * 3).Select(i => (byte)(i * 7)).ToArray();
        var (w, h, back) = PngImage.Decode(PngImage.Encode(7, 5, rgb));
        T.Check("png: round trip", w == 7 && h == 5 && back.SequenceEqual(rgb));
        T.Check("png: rejects other files", T.Throws<InvalidDataException>(() => PngImage.Decode(new byte[32])));
    }

    static void Texture()
    {
        const int size = 4, headerSize = 0x50, dataSize = size * size * 2;
        var d = new byte[0x1c + headerSize + dataSize];
        Encoding.ASCII.GetBytes("NTP3").CopyTo(d, 12);
        BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(18), 1);
        int p = 0x1c;
        BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(p), headerSize + dataSize);
        BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(p + 8), dataSize);
        BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(p + 12), headerSize);
        d[p + 0x11] = 1;
        d[p + 0x13] = Ntp3Texture.Rgb565;
        BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(p + 0x14), size);
        BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(p + 0x16), size);
        var x = XfbinFile.Parse(XfbinTests.Synthetic("tex", d));
        var tex = Ntp3Texture.FindAll(x).Single();
        var rgb = tex.DecodeRgb(x);
        T.Check("ntp3: unchanged image is not rewritten", !tex.Replace(x, size, size, rgb));
        rgb[0] = 255;
        T.Check("ntp3: changed image is written", tex.Replace(x, size, size, rgb) && tex.DecodeRgb(x)[0] == 255);
        var doubled = ImageResize.Area(size, size, rgb, size * 2, size * 2);
        T.Check("ntp3: other size is resampled to the texture size", !tex.Replace(x, size * 2, size * 2, doubled));
        T.Check("resize: area average of 2x1 to 1x1", ImageResize.Area(2, 1, [0, 0, 0, 255, 255, 255], 1, 1).All(v => v is 127 or 128));
    }

    static void Catalog()
    {
        var list = CharacterCatalog.Parse("""[ { "id": 14, "code": "2hnt", "costumes": ["2hnt", "3hnt", "onhn"] }, { "id": 18, "code": "2kks", "costumes": ["2kks"] } ]""");
        var c = new CharacterCatalog(list, true);
        T.Check("catalog: search by name", c.Search("hina").Single().Code == "2hnt");
        T.Check("catalog: search by costume code", c.Search("onhn").Single().Code == "2hnt");
        T.Check("catalog: owner of a costume", c.OwnerOf("3HNT")?.Id == 14);
        T.Check("catalog: names by family", CharacterNames.For("6hnt") == "Hinata" && CharacterNames.For("zzzz") == null);
    }

    static void RealModel()
    {
        var game = XfbinTests.FindGameDir();
        if (game == null)
        {
            Console.WriteLine("SKIP formats: real model tests (not inside the game folder)");
            return;
        }
        var file = new GameArchives(game).Find("data/spc/2hntbod1.xfbin").SingleOrDefault();
        if (file == null)
        {
            Console.WriteLine("SKIP formats: 2hntbod1 not in the archives");
            return;
        }
        var data = file.ReadPlain();
        T.Check("archive: Hinata model decrypts to NUCC", XfbinFile.HasMagic(data));
        var dir = Path.Combine(Path.GetTempPath(), "kw-model-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var x = XfbinFile.Parse(data);
            var (obj, polys, _) = ModelFolder.Export(x, "2hntbod1.xfbin", Path.Combine(dir, "model"));
            TextureFolder.Export(x, Path.Combine(dir, "textures"));
            T.Check("model: polygon groups exported", polys.Count > 10);
            T.Check("xfbin: page-aware chunk names", x.Describe(2).Contains("nuccChunkTexture") && x.Chunks.Select((_, i) => x.TypeOf(i)).Contains("nuccChunkMaterial"));
            var withTextures = ModelFolder.Export(x, "2hntbod1.xfbin", Path.Combine(dir, "model2"), "../textures");
            var mtl = File.ReadAllText(Path.Combine(dir, "model2", "2hntbod1.mtl"));
            T.Check("model: OBJ materials link the exported textures", mtl.Contains("map_Kd ../textures/tex_") && withTextures.polys.Count == polys.Count);
            var r = ModelFolder.Import(x, obj, normals: false);
            TextureFolder.Import(x, Path.Combine(dir, "textures"));
            T.Check("model: unchanged OBJ/PNG round trip is byte-identical", r.Changed.Count == 0 && x.ToBytes().AsSpan().SequenceEqual(data));
            var lines = File.ReadAllLines(obj);
            var poly = polys.First(p => p.Name.Contains("body"));
            int first = Array.FindIndex(lines, l => l == "o " + poly.ObjName) + 1;
            var v = lines[first].Split(' ');
            lines[first] = $"v {float.Parse(v[1]) + 5} {v[2]} {v[3]}";
            File.WriteAllLines(obj, lines);
            var edited = ModelFolder.Import(x, obj, normals: false);
            T.Check("model: one moved vertex is imported", edited.Changed.Sum(c => c.Moved) >= 1);
            var reordered = File.ReadAllLines(obj).ToList();
            int at = reordered.FindIndex(l => l == "o " + poly.ObjName), end = reordered.FindIndex(at + 1, l => l.StartsWith("o ")), faces = reordered.FindIndex(at, l => l.StartsWith("f "));
            if (end < 0) end = reordered.Count;
            reordered.Reverse(faces, end - faces);
            File.WriteAllLines(obj, reordered);
            T.Check("model: reordered faces still match by UV", ModelFolder.Import(x, obj, normals: false).Changed.Sum(c => c.Moved) <= edited.Changed.Sum(c => c.Moved));
            Topology(XfbinFile.Parse(data), dir, poly);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    static bool SameWinding(XfbinFile y, NudPolygon p, List<string> objLines, List<string> faces)
    {
        var v = objLines.Where(l => l.StartsWith("v ")).Select(l => ObjSpace.PositionToGame(l.Split(' ').Skip(1).Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray())).ToList();
        var d = y.Chunks[p.Chunk].Data;
        var tris = p.Triangles().ToList();
        for (int k = 0; k < faces.Count; k++)
        {
            var face = faces[k].Split(' ').Skip(1).Select(c => v[int.Parse(c.Split('/')[0]) - 1]).ToArray();
            var (a, b, c) = tris[k];
            float[][] game = [p.Position(d, a), p.Position(d, b), p.Position(d, c)];
            bool rotated = Enumerable.Range(0, 3).Any(r => Enumerable.Range(0, 3).All(j => Enumerable.Range(0, 3).All(i => MathF.Abs(game[j][i] - face[(j + r) % 3][i]) < 1e-3f)));
            if (!rotated) return false;
        }
        return true;
    }

    static void Topology(XfbinFile x, string dir, NudPolygon body)
    {
        var (obj, polys, _) = ModelFolder.Export(x, "2hntbod1.xfbin", Path.Combine(dir, "topology"));
        var lines = File.ReadAllLines(obj).ToList();
        int at = lines.IndexOf("o " + body.ObjName), end = lines.FindIndex(at + 1, l => l.StartsWith("o "));
        if (end < 0) end = lines.Count;
        var bodyFaces = Enumerable.Range(at, end - at).Where(i => lines[i].StartsWith("f ")).ToList();
        int kept = bodyFaces.Count - bodyFaces.Count / 2;
        var keptFaces = bodyFaces.Take(kept).Select(i => lines[i]).ToList();
        foreach (int i in bodyFaces.Skip(kept).Reverse()) lines.RemoveAt(i);
        var victim = polys.First(p => p.ObjName != body.ObjName && p.Triangles().Any());
        int vAt = lines.IndexOf("o " + victim.ObjName), vEnd = lines.FindIndex(vAt + 1, l => l.StartsWith("o "));
        if (vEnd < 0) vEnd = lines.Count;
        foreach (int i in Enumerable.Range(vAt, vEnd - vAt).Where(i => i == vAt || lines[i].StartsWith("f ")).Reverse()) lines.RemoveAt(i);
        var corner = lines.First(l => l.StartsWith("f ")).Split(' ')[1];
        lines.Add("o NewPart");
        lines.Add("v 0 1.5 0");
        lines.Add("v 0.1 1.5 0");
        lines.Add("v 0 1.6 0");
        lines.Add("vt 0.5 0.5");
        lines.Add("f -3/-1 -2/-1 -1/-1");
        File.WriteAllLines(obj, lines);
        var r = ModelFolder.Import(x, obj, normals: false);
        T.Check("topology: deleted faces rebuild the polygon group", r.Rebuilt.Contains(body.ObjName));
        T.Check("topology: a new object is merged into a part", r.Added.Count == 1 && r.Added[0].Name == "NewPart");
        var y = XfbinFile.Parse(x.ToBytes());
        var after = NudReader.Read(y);
        T.Check("topology: rebuilt file still reads", after.Count == polys.Count);
        var target = r.Added[0].Into;
        int expected = target == body.ObjName ? kept + 1 : kept;
        T.Check("topology: body keeps the remaining faces", after.First(p => p.ObjName == body.ObjName).Triangles().Count() == expected);
        T.Check("topology: rebuilt triangles keep order and winding", SameWinding(y, after.First(p => p.ObjName == body.ObjName), lines, keptFaces));
        T.Check("topology: removed object has no faces", r.Removed.Count > 0 && r.Removed.All(n => !after.First(p => p.ObjName == n).Triangles().Any()));
        var (again, _, _) = ModelFolder.Export(y, "2hntbod1.xfbin", Path.Combine(dir, "topology2"));
        var before = y.ToBytes();
        var second = ModelFolder.Import(y, again, normals: false);
        T.Check("topology: re-export of a rebuilt model imports unchanged", second.Changed.Count == 0 && second.Rebuilt.Count == 0 && y.ToBytes().AsSpan().SequenceEqual(before));
    }
}
