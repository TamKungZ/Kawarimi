using System.Buffers.Binary;

namespace Kawarimi.Formats;

public static class ModelTextures
{
    public static Ntp3Texture? For(XfbinFile x, NudPolygon p, IReadOnlyList<Ntp3Texture> textures)
    {
        var materials = Materials(x, p.Chunk);
        if (materials.Count == 0) return null;
        int material = materials[Math.Min(p.Index, materials.Count - 1)];
        foreach (int chunk in TextureChunks(x, material))
            if (textures.FirstOrDefault(t => t.Chunk == chunk && t.IsSupported) is { } tex) return tex;
        return null;
    }

    static List<int> Materials(XfbinFile x, int modelChunk)
    {
        var d = x.Chunks[modelChunk].Data;
        int b = d.AsSpan().IndexOf("NDP3"u8);
        var list = new List<int>();
        if (b < 0 || b + 8 > d.Length) return list;
        int tail = b + (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(b + 4));
        if (tail + 2 > d.Length) return list;
        int count = BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(tail));
        for (int i = 0; i < count && tail + 6 + 4 * i <= d.Length; i++)
        {
            uint reference = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(tail + 2 + 4 * i));
            if (x.Reference(modelChunk, reference) is { } m && x.TypeOf(m) == "nuccChunkMaterial") list.Add(m);
        }
        return list;
    }

    static IEnumerable<int> TextureChunks(XfbinFile x, int material)
    {
        var d = x.Chunks[material].Data;
        for (int o = 0; o + 4 <= d.Length; o += 4)
        {
            uint value = BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
            if (value > 0xFFFF) continue;
            if (x.Reference(material, value) is { } t && x.TypeOf(t) == "nuccChunkTexture") yield return t;
        }
    }
}
