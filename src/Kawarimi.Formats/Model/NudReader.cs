using System.Buffers.Binary;
using System.Text;

namespace Kawarimi.Formats;

public static class NudReader
{
    public static List<NudPolygon> Read(XfbinFile x, List<string>? warnings = null)
    {
        var list = new List<NudPolygon>();
        for (int c = 0; c < x.Chunks.Count; c++)
        {
            var d = x.Chunks[c].Data;
            int b = d.AsSpan().IndexOf("NDP3"u8);
            if (b >= 0 && b + 0x30 <= d.Length) ReadNud(d, c, b, list, warnings);
        }
        return list;
    }

    static void ReadNud(byte[] d, int chunk, int b, List<NudPolygon> list, List<string>? warnings)
    {
        int meshes = U16(d, b + 10), polyStart = b + I32(d, b + 16) + 0x30;
        int vertStart = polyStart + I32(d, b + 20), addStart = vertStart + I32(d, b + 24), nameStart = addStart + I32(d, b + 28);
        for (int m = 0; m < meshes; m++)
        {
            int mo = b + 0x30 + m * 0x30;
            int nameOff = nameStart + I32(d, mo + 32);
            string name = Encoding.ASCII.GetString(d, nameOff, Array.IndexOf(d, (byte)0, nameOff) - nameOff);
            int polys = U16(d, mo + 42), ph = I32(d, mo + 44);
            for (int p = 0; p < polys; p++)
            {
                try { list.Add(ReadPolygon(d, chunk, b + ph + p * 0x30, name, m, p, polyStart, vertStart, addStart)); }
                catch (Exception e) { warnings?.Add($"chunk {chunk} mesh {name} polygon {p}: skipped ({e.Message})"); }
            }
        }
    }

    static NudPolygon ReadPolygon(byte[] d, int chunk, int q, string name, int set, int index, int polyStart, int vertStart, int addStart)
    {
        int vc = U16(d, q + 12), vt = d[q + 14], uvt = d[q + 15], ic = U16(d, q + 0x20);
        int weight = vt >> 4, nrm = vt & 15, uvCount = uvt >> 4, color = ColorSize(uvt & 15);
        var (nb, no) = NormalBlock(nrm);
        int posOff, posStride, uvOff, uvStride;
        if (weight > 0)
        {
            posOff = addStart + I32(d, q + 8);
            posStride = nb + BoneBlock(weight);
            uvStride = color + 4 * uvCount;
            uvOff = vertStart + I32(d, q + 4) + color;
        }
        else
        {
            posOff = vertStart + I32(d, q + 4);
            posStride = nb + color + 4 * uvCount;
            uvStride = posStride;
            uvOff = posOff + nb + color;
        }
        if (posOff + vc * posStride > d.Length) throw new InvalidDataException("vertex data out of range");
        var idx = new ushort[ic];
        int io = polyStart + I32(d, q);
        for (int i = 0; i < ic; i++) idx[i] = U16(d, io + 2 * i);
        int vertOff = weight > 0 ? vertStart + I32(d, q + 4) : posOff;
        if (vertOff + vc * uvStride > d.Length) throw new InvalidDataException("vertex data out of range");
        return new NudPolygon(chunk, name, set, index, vc, posOff, posStride, nrm, no, uvOff, uvStride, uvCount, idx, q, vertOff, uvStride, weight > 0);
    }

    static (int size, int nrm) NormalBlock(int kind) => kind switch
    {
        0 => (16, -1),
        1 => (32, 16),
        2 => (64, 16),
        3 => (64, 16),
        6 => (20, 12),
        7 => (36, 12),
        _ => throw new InvalidDataException("normal type " + kind),
    };

    static int BoneBlock(int kind) => kind switch { 0 => 0, 1 => 32, 2 => 16, 4 => 8, _ => throw new InvalidDataException("weight type " + kind) };
    static int ColorSize(int kind) => kind switch { 0 => 0, 2 => 4, 4 => 8, _ => throw new InvalidDataException("colour type " + kind) };
    static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o));
    static int I32(byte[] d, int o) => (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
}
