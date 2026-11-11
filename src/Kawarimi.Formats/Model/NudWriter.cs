using System.Buffers.Binary;

namespace Kawarimi.Formats;

public static class NudWriter
{
    const int Align = 16;

    public static byte[] Rewrite(byte[] d, IReadOnlyList<NudPolygon> polys, IReadOnlyDictionary<int, NudPolygonData> replaced)
    {
        int b = d.AsSpan().IndexOf("NDP3"u8);
        int size = I32(d, b + 4), meshes = U16(d, b + 10);
        int polyStart = b + I32(d, b + 16) + 0x30, vertStart = polyStart + I32(d, b + 20);
        int addStart = vertStart + I32(d, b + 24), nameStart = addStart + I32(d, b + 28);
        var byHeader = polys.ToDictionary(p => p.Header);
        var head = d[..polyStart];
        using var index = new MemoryStream();
        using var vert = new MemoryStream();
        using var add = new MemoryStream();
        var all = new List<float[]>();
        for (int m = 0; m < meshes; m++)
        {
            int mo = b + 0x30 + m * 0x30;
            int count = U16(d, mo + 42), ph = I32(d, mo + 44);
            var meshPoints = new List<float[]>();
            for (int k = 0; k < count; k++)
            {
                int q = b + ph + k * 0x30;
                if (!byHeader.TryGetValue(q, out var p))
                    throw new InvalidDataException($"polygon {k} of mesh {m} could not be read, so this model cannot be rebuilt");
                int io = (int)index.Length, vo = (int)vert.Length, ao = (int)add.Length;
                if (replaced.TryGetValue(q, out var r))
                {
                    index.Write(r.Indices);
                    vert.Write(r.Vertices);
                    add.Write(r.Additional);
                    W16(head, q + 12, r.VertexCount);
                    W16(head, q + 0x20, r.IndexCount);
                    meshPoints.AddRange(r.Positions);
                }
                else
                {
                    index.Write(d, polyStart + I32(d, q), p.Indices.Length * 2);
                    vert.Write(d, p.VertOffset, p.VertCount * p.VertStride);
                    if (p.Skinned) add.Write(d, p.PosOffset, p.VertCount * p.PosStride);
                }
                W32(head, q, io);
                W32(head, q + 4, vo);
                if (p.Skinned) W32(head, q + 8, ao);
            }
            GrowSphere(head, mo, meshPoints);
            all.AddRange(meshPoints);
        }
        Pad(index);
        Pad(vert);
        Pad(add);
        GrowSphere(head, b + 0x20, all);
        if (b >= 52) GrowBox(head, 28, all);
        W32(head, b + 20, (int)index.Length);
        W32(head, b + 24, (int)vert.Length);
        W32(head, b + 28, (int)add.Length);
        int names = b + size - nameStart;
        int newSize = polyStart - b + (int)(index.Length + vert.Length + add.Length) + names;
        W32(head, b + 4, newSize);
        if (b >= 28 && I32(d, 24) == size) W32(head, 24, newSize);
        using var o = new MemoryStream();
        o.Write(head);
        o.Write(index.ToArray());
        o.Write(vert.ToArray());
        o.Write(add.ToArray());
        o.Write(d, nameStart, names);
        o.Write(d, b + size, d.Length - (b + size));
        return o.ToArray();
    }

    static void GrowSphere(byte[] d, int o, List<float[]> points)
    {
        float cx = F32(d, o), cy = F32(d, o + 4), cz = F32(d, o + 8), r = F32(d, o + 12);
        foreach (var p in points)
        {
            float x = p[0] - cx, y = p[1] - cy, z = p[2] - cz;
            r = MathF.Max(r, MathF.Sqrt(x * x + y * y + z * z));
        }
        BinaryPrimitives.WriteSingleBigEndian(d.AsSpan(o + 12), r);
    }

    static void GrowBox(byte[] d, int o, List<float[]> points)
    {
        foreach (var p in points)
            for (int k = 0; k < 3; k++)
            {
                BinaryPrimitives.WriteSingleBigEndian(d.AsSpan(o + 4 * k), MathF.Min(F32(d, o + 4 * k), p[k]));
                BinaryPrimitives.WriteSingleBigEndian(d.AsSpan(o + 12 + 4 * k), MathF.Max(F32(d, o + 12 + 4 * k), p[k]));
            }
    }

    static void Pad(MemoryStream s)
    {
        while (s.Length % Align != 0) s.WriteByte(0);
    }

    static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o));
    static int I32(byte[] d, int o) => (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
    static float F32(byte[] d, int o) => BinaryPrimitives.ReadSingleBigEndian(d.AsSpan(o));
    static void W16(byte[] d, int o, int v) => BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(o), (ushort)v);
    static void W32(byte[] d, int o, int v) => BinaryPrimitives.WriteUInt32BigEndian(d.AsSpan(o), (uint)v);
}
