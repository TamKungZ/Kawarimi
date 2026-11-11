using System.Buffers.Binary;

namespace Kawarimi.Formats;

public sealed record NudPolygonData(byte[] Indices, byte[] Vertices, byte[] Additional, int VertexCount, int IndexCount, List<float[]> Positions);

public static class NudPolygonBuilder
{
    const int MaxIndex = 0xFFFE;
    const float UvWeight = 1e-3f;

    public static NudPolygonData Build(NudPolygon p, byte[] d, IReadOnlyList<ObjCorner[]> faces, ObjDocument doc)
    {
        var map = new Dictionary<ObjCorner, int>();
        var corners = new List<ObjCorner>();
        var tris = new List<(int, int, int)>();
        foreach (var f in faces)
        {
            if (f.Length < 3) continue;
            var ix = f.Select(c =>
            {
                if (!map.TryGetValue(c, out int i))
                {
                    map[c] = i = corners.Count;
                    corners.Add(c);
                }
                return i;
            }).ToArray();
            for (int k = 1; k + 1 < ix.Length; k++) tris.Add((ix[0], ix[k], ix[k + 1]));
        }
        if (corners.Count == 0) return Empty(p, d);
        if (corners.Count > MaxIndex)
            throw new InvalidDataException($"{p.ObjName}: {corners.Count} vertices, at most {MaxIndex} fit in one polygon group. Split the object or remove detail.");

        var origPos = Enumerable.Range(0, p.VertCount).Select(i => p.Position(d, i)).ToArray();
        var origUv = Enumerable.Range(0, p.VertCount).Select(i => p.ObjUv(d, i)).ToArray();
        var target = p.InBuffer(corners.Count);
        var buffer = new byte[target.BufferSize];
        var positions = new List<float[]>(corners.Count);
        for (int j = 0; j < corners.Count; j++)
        {
            var c = corners[j];
            var pos = doc.V[c.V];
            float[]? uv = c.T >= 0 ? doc.VT[c.T] : null;
            p.CopyVertex(d, Nearest(origPos, origUv, pos, uv), target, buffer, j);
            target.SetPosition(buffer, j, pos);
            if (p.HasNormals && c.N >= 0 && Normalize(doc.N[c.N]) is { } n) target.SetNormal(buffer, j, n);
            if (uv != null) target.SetObjUv(buffer, j, uv);
            positions.Add(pos);
        }
        var (indices, count) = Strip(tris);
        if (count > 0xFFFF)
            throw new InvalidDataException($"{p.ObjName}: {tris.Count} triangles need {count} indices, at most 65535 fit in one polygon group. Remove detail.");
        int vertLen = corners.Count * p.VertStride;
        return new NudPolygonData(indices, buffer[..vertLen], buffer[vertLen..], corners.Count, count, positions);
    }

    public static NudPolygonData Empty(NudPolygon p, byte[] d)
    {
        var target = p.InBuffer(1);
        var buffer = new byte[target.BufferSize];
        p.CopyVertex(d, 0, target, buffer, 0);
        var indices = new byte[6];
        int vertLen = p.VertStride;
        return new NudPolygonData(indices, buffer[..vertLen], buffer[vertLen..], 1, 3, new());
    }

    static int Nearest(float[][] pos, float[][] uv, float[] p, float[]? t)
    {
        int best = 0;
        float bestScore = float.MaxValue;
        for (int i = 0; i < pos.Length; i++)
        {
            float x = pos[i][0] - p[0], y = pos[i][1] - p[1], z = pos[i][2] - p[2];
            float score = x * x + y * y + z * z;
            if (t != null)
            {
                float u = uv[i][0] - t[0], v = uv[i][1] - t[1];
                score += UvWeight * (u * u + v * v);
            }
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }
        return best;
    }

    static (byte[], int) Strip(List<(int, int, int)> tris)
    {
        var s = new List<int>();
        int run = 0;
        foreach (var (a, b, c) in tris)
        {
            if (run >= 2 && Continue(a, b, c, s[^2], s[^1], (run + 1) % 2 == 1) is int z)
            {
                s.Add(z);
                run++;
                continue;
            }
            if (s.Count > 0) s.Add(0xFFFF);
            s.Add(a);
            s.Add(b);
            s.Add(c);
            run = 3;
        }
        var bytes = new byte[s.Count * 2];
        for (int i = 0; i < s.Count; i++) BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2 * i), (ushort)s[i]);
        return (bytes, s.Count);
    }

    static int? Continue(int a, int b, int c, int x, int y, bool odd)
    {
        var (p0, p1) = odd ? (x, y) : (y, x);
        if (a == p0 && b == p1) return c;
        if (b == p0 && c == p1) return a;
        if (c == p0 && a == p1) return b;
        return null;
    }

    static float[]? Normalize(float[] n)
    {
        float len = MathF.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
        return len < 1e-6f ? null : [n[0] / len, n[1] / len, n[2] / len];
    }
}
