namespace Kawarimi.Formats;

public sealed record MeshImportResult(List<(string Name, int Moved)> Changed, List<string> Rebuilt, List<string> Removed, List<(string Name, string Into)> Added);

public static class MeshImporter
{
    const float MoveEpsilon2 = 1e-6f;
    const float NormalEpsilon = 2e-3f;
    const float FaceOrderUvTolerance = 3e-3f;

    public static MeshImportResult Apply(XfbinFile x, ObjDocument doc, bool normals)
    {
        var polys = NudReader.Read(x);
        var result = new MeshImportResult(new(), new(), new(), new());
        var names = polys.Select(p => p.ObjName).ToHashSet();
        if (!doc.Objects.Keys.Any(names.Contains)) return result;

        var faces = polys.ToDictionary(p => p, p => doc.Objects.TryGetValue(p.ObjName, out var o) ? o.Faces.ToList() : null);
        foreach (var (name, extra) in doc.Objects.Where(kv => !names.Contains(kv.Key) && kv.Value.Faces.Count > 0))
        {
            var target = NearestPolygon(x, polys.Where(p => faces[p] != null), extra, doc);
            if (target == null) continue;
            faces[target]!.AddRange(extra.Faces);
            result.Added.Add((name, target.ObjName));
        }

        var rebuilt = new Dictionary<int, Dictionary<int, NudPolygonData>>();
        foreach (var p in polys)
        {
            var d = x.Chunks[p.Chunk].Data;
            var f = faces[p];
            NudPolygonData data;
            if (f == null)
            {
                if (!p.Triangles().Any()) continue;
                data = NudPolygonBuilder.Empty(p, d);
                result.Removed.Add(p.ObjName);
            }
            else if (TryInPlace(p, d, f, doc, normals, result)) continue;
            else
            {
                data = NudPolygonBuilder.Build(p, d, f, doc);
                result.Rebuilt.Add(p.ObjName);
                result.Changed.Add((p.ObjName, data.VertexCount));
            }
            if (!rebuilt.TryGetValue(p.Chunk, out var map)) rebuilt[p.Chunk] = map = new();
            map[p.Header] = data;
        }
        foreach (var (chunk, map) in rebuilt)
            x.Chunks[chunk].Data = NudWriter.Rewrite(x.Chunks[chunk].Data, polys.Where(p => p.Chunk == chunk).ToList(), map);
        return result;
    }

    static bool TryInPlace(NudPolygon p, byte[] d, List<ObjCorner[]> faces, ObjDocument doc, bool normals, MeshImportResult result)
    {
        var tris = p.Triangles().ToList();
        if (faces.Sum(f => Math.Max(0, f.Length - 2)) != tris.Count) return false;
        bool hasUv = p.UvCount > 0 && faces.Count > 0 && faces.All(f => f.All(c => c.T >= 0));
        var orig = Enumerable.Range(0, p.VertCount).Select(i => p.Position(d, i)).ToArray();
        var pos = new float[]?[p.VertCount];
        var nrm = new float[]?[p.VertCount];
        if (faces.All(f => f.Length == 3) && FaceOrderMatches(p, d, tris, faces, doc, hasUv))
            MatchByFaceOrder(p, d, orig, tris, faces, doc, hasUv, pos, nrm);
        else if (!hasUv || !MatchByUv(p, d, orig, faces, doc, pos, nrm) || tris.Any(t => pos[t.Item1] == null || pos[t.Item2] == null || pos[t.Item3] == null))
            return false;
        int moved = Write(p, d, orig, pos, normals ? nrm : null);
        if (moved > 0) result.Changed.Add((p.ObjName, moved));
        return true;
    }

    static NudPolygon? NearestPolygon(XfbinFile x, IEnumerable<NudPolygon> polys, ObjObject extra, ObjDocument doc)
    {
        var used = extra.Faces.SelectMany(f => f).Select(c => doc.V[c.V]).ToList();
        float[] center = [used.Average(v => v[0]), used.Average(v => v[1]), used.Average(v => v[2])];
        NudPolygon? best = null;
        float bestDist = float.MaxValue;
        foreach (var p in polys)
        {
            var d = x.Chunks[p.Chunk].Data;
            foreach (var (a, _, _) in p.Triangles())
            {
                float dist = Dist2(p.Position(d, a), center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = p;
                }
            }
        }
        return best;
    }

    static bool FaceOrderMatches(NudPolygon p, byte[] d, List<(int, int, int)> tris, List<ObjCorner[]> faces, ObjDocument doc, bool hasUv)
    {
        if (!hasUv) return true;
        for (int k = 0; k < tris.Count; k++)
        {
            var (a, b, c) = tris[k];
            int[] g = [a, b, c];
            var f = faces[k];
            float best = float.MaxValue;
            for (int r = 0; r < 3; r++)
            {
                float e = 0;
                for (int j = 0; j < 3; j++)
                {
                    var gu = p.ObjUv(d, g[j]);
                    var fu = doc.VT[f[(j + r) % 3].T];
                    e += MathF.Abs(gu[0] - fu[0]) + MathF.Abs(gu[1] - fu[1]);
                }
                best = MathF.Min(best, e);
            }
            if (best > FaceOrderUvTolerance) return false;
        }
        return true;
    }

    static void MatchByFaceOrder(NudPolygon p, byte[] d, float[][] orig, List<(int, int, int)> tris, List<ObjCorner[]> faces, ObjDocument doc, bool hasUv,
                                 float[]?[] pos, float[]?[] nrm)
    {
        for (int k = 0; k < tris.Count; k++)
        {
            var (a, b, c) = tris[k];
            int[] g = [a, b, c];
            var f = faces[k];
            int rot = hasUv ? BestRotation(p, d, orig, g, f, doc) : 0;
            for (int j = 0; j < 3; j++)
            {
                var cr = f[(j + rot) % 3];
                pos[g[j]] = doc.V[cr.V];
                if (cr.N >= 0) nrm[g[j]] = doc.N[cr.N];
            }
        }
    }

    static int BestRotation(NudPolygon p, byte[] d, float[][] orig, int[] g, ObjCorner[] f, ObjDocument doc)
    {
        int rot = 0;
        float best = float.MaxValue;
        for (int r = 0; r < 3; r++)
        {
            float e = 0;
            for (int j = 0; j < 3; j++)
            {
                var cr = f[(j + r) % 3];
                var gu = p.ObjUv(d, g[j]);
                var fu = doc.VT[cr.T];
                e += MathF.Abs(gu[0] - fu[0]) + MathF.Abs(gu[1] - fu[1]) + 1e-4f * MathF.Sqrt(Dist2(orig[g[j]], doc.V[cr.V]));
            }
            if (e < best)
            {
                best = e;
                rot = r;
            }
        }
        return rot;
    }

    static bool MatchByUv(NudPolygon p, byte[] d, float[][] orig, List<ObjCorner[]> faces, ObjDocument doc, float[]?[] pos, float[]?[] nrm)
    {
        var byUv = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < p.VertCount; i++)
        {
            var uv = p.ObjUv(d, i);
            var key = UvKey(uv[0], uv[1]);
            if (!byUv.TryGetValue(key, out var l)) byUv[key] = l = new();
            l.Add(i);
        }
        foreach (var c in faces.SelectMany(f => f))
        {
            var (ku, kv) = UvKey(doc.VT[c.T][0], doc.VT[c.T][1]);
            var cand = new List<int>();
            for (int du = -1; du <= 1; du++)
                for (int dv = -1; dv <= 1; dv++)
                    if (byUv.TryGetValue((ku + du, kv + dv), out var l)) cand.AddRange(l);
            if (cand.Count == 0) return false;
            var target = doc.V[c.V];
            int best = cand.OrderBy(i => Dist2(orig[i], target)).First();
            foreach (int i in cand.Where(i => Dist2(orig[i], orig[best]) < 1e-8f))
            {
                pos[i] = target;
                if (c.N >= 0) nrm[i] = doc.N[c.N];
            }
        }
        return true;
    }

    static int Write(NudPolygon p, byte[] d, float[][] orig, float[]?[] pos, float[]?[]? nrm)
    {
        int moved = 0;
        for (int i = 0; i < p.VertCount; i++)
        {
            if (pos[i] is { } np && Dist2(np, orig[i]) > MoveEpsilon2)
            {
                p.SetPosition(d, i, np);
                moved++;
            }
            if (nrm == null || !p.HasNormals || nrm[i] is not { } n) continue;
            var unit = Normalize(n);
            var old = Normalize(p.Normal(d, i));
            if (unit == null || (old != null && Enumerable.Range(0, 3).All(k => MathF.Abs(old[k] - unit[k]) < NormalEpsilon))) continue;
            p.SetNormal(d, i, unit);
        }
        return moved;
    }

    static float[]? Normalize(float[] n)
    {
        float len = MathF.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
        return len < 1e-6f ? null : [n[0] / len, n[1] / len, n[2] / len];
    }

    static (int, int) UvKey(float u, float v) => ((int)MathF.Round(u * 4096), (int)MathF.Round(v * 4096));

    static float Dist2(float[] a, float[] b)
    {
        float x = a[0] - b[0], y = a[1] - b[1], z = a[2] - b[2];
        return x * x + y * y + z * z;
    }
}
