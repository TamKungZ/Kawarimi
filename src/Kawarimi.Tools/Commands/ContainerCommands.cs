using System.Text;
using System.Text.Json;
using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class ContainerCommands
{
    const string ManifestFile = "manifest.json";
    const string HeaderFile = "header.bin";
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    record UnpackManifest(uint Version, string Header, List<UnpackedChunk> Chunks);
    record UnpackedChunk(string File, uint MapIndex, ushort Version, ushort Flags, string Describe);

    public static int Info(string path)
    {
        var x = XfbinFile.Parse(File.ReadAllBytes(path));
        Ui.Info($"{Path.GetFileName(path)}: version 0x{x.Version:x}, {x.Chunks.Count} chunk(s), header {x.Header.Length} bytes");
        if (x.PathNames.Count > 0) Ui.Info("paths: " + string.Join(", ", x.PathNames));
        for (int i = 0; i < x.Chunks.Count; i++)
        {
            var c = x.Chunks[i];
            Ui.Info($"  [{i}] {x.Describe(c),-48} {c.Data.Length,10} bytes  {Peek(c.Data)}");
        }
        return 0;
    }

    public static int Unpack(string xfbin, string dir)
    {
        var x = XfbinFile.Parse(File.ReadAllBytes(xfbin));
        Directory.CreateDirectory(dir);
        var chunks = new List<UnpackedChunk>();
        for (int i = 0; i < x.Chunks.Count; i++)
        {
            var c = x.Chunks[i];
            string file = $"{i:D3}_{NudPolygon.SafeName(x.Describe(c))}.bin";
            File.WriteAllBytes(Path.Combine(dir, file), c.Data);
            chunks.Add(new UnpackedChunk(file, c.MapIndex, c.Version, c.Flags, x.Describe(c)));
        }
        File.WriteAllBytes(Path.Combine(dir, HeaderFile), x.Header);
        File.WriteAllText(Path.Combine(dir, ManifestFile), JsonSerializer.Serialize(new UnpackManifest(x.Version, HeaderFile, chunks), Pretty));
        Ui.Ok($"Unpacked {chunks.Count} chunk(s) to {dir}");
        return 0;
    }

    public static int Pack(string dir, string outFile)
    {
        var m = JsonSerializer.Deserialize<UnpackManifest>(File.ReadAllText(Path.Combine(dir, ManifestFile))) ?? throw new InvalidDataException("bad manifest");
        var ms = new MemoryStream();
        ms.Write(File.ReadAllBytes(Path.Combine(dir, m.Header)));
        var h = new byte[12];
        foreach (var c in m.Chunks)
        {
            var data = File.ReadAllBytes(Path.Combine(dir, c.File));
            XfbinFile.WriteChunkHeader(h, data.Length, c.MapIndex, c.Version, c.Flags);
            ms.Write(h);
            ms.Write(data);
        }
        var bytes = ms.ToArray();
        XfbinFile.Parse(bytes);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllBytes(outFile, bytes);
        Ui.Ok($"Packed {m.Chunks.Count} chunk(s) -> {outFile} ({bytes.Length} bytes)");
        return 0;
    }

    public static int Retarget(string input, string oldCode, string newCode, string output)
    {
        int n = SkinBuilder.RetargetFile(input, oldCode, newCode, output);
        Ui.Ok($"{Path.GetFileName(input)} -> {Path.GetFileName(output)} ({n} replacement(s))");
        return 0;
    }

    static string Peek(byte[] d) =>
        d.Length >= 4 && d.Take(4).All(b => b is >= 0x20 and < 0x7f) ? "\"" + Encoding.ASCII.GetString(d, 0, 4) + "\"" : "";
}
