using System.Buffers.Binary;

namespace Kawarimi.Formats;

public sealed record NudPolygon(int Chunk, string Name, int Set, int Index, int VertCount, int PosOffset, int PosStride,
                                int NrmKind, int NrmOffset, int UvOffset, int UvStride, int UvCount, ushort[] Indices,
                                int Header, int VertOffset, int VertStride, bool Skinned)
{
    public string ObjName => $"c{Chunk:D3}_{Set}_{Index}_{SafeName(Name)}";
    public bool HasNormals => NrmOffset >= 0;
    bool HalfNormals => NrmKind >= 6;

    public static string SafeName(string s) => new(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

    public NudPolygon InBuffer(int vertexCount) => Skinned
        ? this with { VertCount = vertexCount, VertOffset = 0, UvOffset = UvOffset - VertOffset, PosOffset = vertexCount * VertStride }
        : this with { VertCount = vertexCount, VertOffset = 0, UvOffset = UvOffset - PosOffset, PosOffset = 0 };

    public int BufferSize => VertCount * VertStride + (Skinned ? VertCount * PosStride : 0);

    public void CopyVertex(byte[] d, int i, NudPolygon target, byte[] buffer, int j)
    {
        Array.Copy(d, VertOffset + i * VertStride, buffer, target.VertOffset + j * VertStride, VertStride);
        if (Skinned) Array.Copy(d, PosOffset + i * PosStride, buffer, target.PosOffset + j * PosStride, PosStride);
    }

    public float[] Position(byte[] d, int i)
    {
        int o = PosOffset + i * PosStride;
        return [F32(d, o), F32(d, o + 4), F32(d, o + 8)];
    }

    public void SetPosition(byte[] d, int i, float[] p)
    {
        int o = PosOffset + i * PosStride;
        for (int k = 0; k < 3; k++) BinaryPrimitives.WriteSingleBigEndian(d.AsSpan(o + 4 * k), p[k]);
    }

    public float[] Normal(byte[] d, int i)
    {
        int o = PosOffset + i * PosStride + NrmOffset;
        return HalfNormals ? [H16(d, o), H16(d, o + 2), H16(d, o + 4)] : [F32(d, o), F32(d, o + 4), F32(d, o + 8)];
    }

    public void SetNormal(byte[] d, int i, float[] n)
    {
        int o = PosOffset + i * PosStride + NrmOffset;
        for (int k = 0; k < 3; k++)
        {
            if (HalfNormals) BinaryPrimitives.WriteHalfBigEndian(d.AsSpan(o + 2 * k), (Half)n[k]);
            else BinaryPrimitives.WriteSingleBigEndian(d.AsSpan(o + 4 * k), n[k]);
        }
    }

    public float[] ObjUv(byte[] d, int i)
    {
        if (UvCount == 0) return [0, 0];
        int o = UvOffset + i * UvStride;
        return [H16(d, o), 1 - H16(d, o + 2)];
    }

    public void SetObjUv(byte[] d, int i, float[] uv)
    {
        if (UvCount == 0) return;
        int o = UvOffset + i * UvStride;
        BinaryPrimitives.WriteHalfBigEndian(d.AsSpan(o), (Half)uv[0]);
        BinaryPrimitives.WriteHalfBigEndian(d.AsSpan(o + 2), (Half)(1 - uv[1]));
    }

    public IEnumerable<(int, int, int)> Triangles()
    {
        int run = 0;
        for (int i = 0; i < Indices.Length; i++)
        {
            if (Indices[i] == 0xFFFF)
            {
                run = 0;
                continue;
            }
            if (++run < 3) continue;
            int a = Indices[i - 2], b = Indices[i - 1], c = Indices[i];
            if (a == b || b == c || a == c) continue;
            yield return (run & 1) == 1 ? (a, b, c) : (b, a, c);
        }
    }

    static float F32(byte[] d, int o) => BinaryPrimitives.ReadSingleBigEndian(d.AsSpan(o));
    static float H16(byte[] d, int o) => (float)BinaryPrimitives.ReadHalfBigEndian(d.AsSpan(o));
}
