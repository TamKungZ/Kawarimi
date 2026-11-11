using System.Security.Cryptography;
using Kawarimi.Formats;

namespace Kawarimi.Tools.Capture;

internal sealed class CaptureNaming
{
    readonly ILookup<long, ArchiveFile> _bySize;
    readonly Dictionary<string, string> _loose = new();

    public CaptureNaming(string gameDir, GameArchives archives)
    {
        _bySize = archives.AllXfbin().ToLookup(f => f.Entry.ExtractSize);
        foreach (var lf in Directory.GetFiles(Path.Combine(gameDir, "data"), "*.xfbin", SearchOption.AllDirectories))
            _loose[Hash(File.ReadAllBytes(lf))] = Path.GetRelativePath(gameDir, lf).Replace('\\', '/');
    }

    public static string Hash(byte[] data) => Convert.ToHexString(SHA1.HashData(data))[..16];

    public (string? path, bool exact, List<string> candidates) Name(XfbinFile x, byte[] file)
    {
        if (_loose.TryGetValue(Hash(file), out var lp)) return (lp, true, [lp]);
        var cands = _bySize[file.Length].GroupBy(c => c.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.Last().Path).ToList();
        if (cands.Count > 1)
        {
            var text = string.Join("\n", x.PathNames.Concat(x.ChunkNames)).ToLowerInvariant();
            var named = cands.Where(c => text.Contains(Stem(c).ToLowerInvariant())).ToList();
            if (named.Count >= 1) cands = named;
        }
        return (cands.Count == 1 ? cands[0] : null, false, cands);
    }

    static string Stem(string path)
    {
        var n = Path.GetFileName(path);
        foreach (var suf in new[] { ".bin.xfbin", ".xfbin" })
            if (n.EndsWith(suf, StringComparison.OrdinalIgnoreCase)) return n[..^suf.Length];
        return n;
    }
}
