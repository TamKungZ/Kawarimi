namespace Kawarimi.Formats;

public static class ModelComparer
{
    public sealed record Result(Dictionary<string, (int A, int B)> TypeCounts, List<string> OnlyInA, List<string> OnlyInB, int Shared)
    {
        public bool SameNames => OnlyInA.Count == 0 && OnlyInB.Count == 0;
    }

    public static Result Compare(string fileA, string codeA, string fileB, string codeB)
    {
        var ca = new Dictionary<string, int>();
        var cb = new Dictionary<string, int>();
        var a = Names(fileA, codeA, ca);
        var b = Names(fileB, codeB, cb);
        var types = ca.Keys.Union(cb.Keys).OrderBy(k => k).ToDictionary(k => k, k => (ca.GetValueOrDefault(k), cb.GetValueOrDefault(k)));
        return new Result(types, a.Except(b).OrderBy(s => s).ToList(), b.Except(a).OrderBy(s => s).ToList(), a.Intersect(b).Count());
    }

    static HashSet<string> Names(string file, string code, Dictionary<string, int> counts)
    {
        var x = XfbinFile.Parse(File.ReadAllBytes(file));
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (t, _, n) in x.Map)
        {
            string type = t >= 0 && t < x.TypeNames.Count ? x.TypeNames[t] : "?";
            string name = n >= 0 && n < x.ChunkNames.Count ? x.ChunkNames[n] : "?";
            name = name.Replace(code, "{code}", StringComparison.OrdinalIgnoreCase);
            counts[type] = counts.GetValueOrDefault(type) + 1;
            set.Add(type + ":" + name);
        }
        return set;
    }
}
