using System.Globalization;

namespace Kawarimi.Formats;

public readonly record struct ObjCorner(int V, int T, int N);

public sealed class ObjObject
{
    public List<ObjCorner[]> Faces { get; } = new();
    public IEnumerable<ObjCorner> Corners => Faces.SelectMany(f => f);
}

public sealed class ObjDocument
{
    public List<float[]> V { get; } = new();
    public List<float[]> VT { get; } = new();
    public List<float[]> N { get; } = new();
    public Dictionary<string, ObjObject> Objects { get; } = new();

    public static ObjDocument Parse(IEnumerable<string> lines, IReadOnlyCollection<string> knownNames)
    {
        var doc = new ObjDocument();
        ObjObject? cur = null;
        foreach (var raw in lines)
        {
            var t = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) continue;
            float P(int i) => float.Parse(t[i], CultureInfo.InvariantCulture);
            switch (t[0])
            {
                case "o":
                case "g":
                    cur = t.Length > 1 ? doc.ObjectFor(Resolve(t[1], knownNames)) : null;
                    break;
                case "v":
                    float[] v = [P(1), P(2), P(3)];
                    doc.V.Add(ObjSpace.PositionToGame(v));
                    break;
                case "vt":
                    doc.VT.Add([P(1), P(2)]);
                    break;
                case "vn":
                    float[] n = [P(1), P(2), P(3)];
                    doc.N.Add(ObjSpace.NormalToGame(n));
                    break;
                case "f" when cur != null:
                    cur.Faces.Add(t.Skip(1).Select(doc.Corner).ToArray());
                    break;
            }
        }
        return doc;
    }

    ObjObject ObjectFor(string name)
    {
        if (!Objects.TryGetValue(name, out var o)) Objects[name] = o = new ObjObject();
        return o;
    }

    ObjCorner Corner(string token)
    {
        var s = token.Split('/');
        int Ix(int k, int count)
        {
            if (s.Length <= k || s[k].Length == 0) return -1;
            int v = int.Parse(s[k]);
            return v < 0 ? count + v : v - 1;
        }
        return new ObjCorner(Ix(0, V.Count), Ix(1, VT.Count), Ix(2, N.Count));
    }

    static string Resolve(string name, IReadOnlyCollection<string> known) =>
        known.Contains(name) ? name : known.Where(k => name.StartsWith(k, StringComparison.Ordinal)).OrderByDescending(k => k.Length).FirstOrDefault() ?? name;
}
