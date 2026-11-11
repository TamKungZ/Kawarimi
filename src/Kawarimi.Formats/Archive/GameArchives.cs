namespace Kawarimi.Formats;

public sealed record ArchiveFile(Cpk Archive, CpkEntry Entry)
{
    public string Path => Entry.Path;
    public byte[] ReadPlain() => Archive.ReadPlain(Entry);
}

public sealed class GameArchives
{
    readonly string _gameDir;
    readonly List<Cpk> _archives = new();

    public GameArchives(string gameDir, Action<string>? warn = null)
    {
        _gameDir = gameDir;
        foreach (var f in ArchivePaths(gameDir))
        {
            try { _archives.Add(new Cpk(f)); }
            catch (Exception ex) { warn?.Invoke($"{System.IO.Path.GetFileName(f)}: {ex.Message}"); }
        }
    }

    public IReadOnlyList<Cpk> Archives => _archives;

    public string RelativePath(Cpk cpk) => System.IO.Path.GetRelativePath(_gameDir, cpk.FilePath);

    public IReadOnlyList<ArchiveFile> Find(string filter)
    {
        var latest = new Dictionary<string, ArchiveFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var cpk in _archives)
            foreach (var e in cpk.Entries.Where(e => e.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                latest[e.Path] = new ArchiveFile(cpk, e);
        return latest.Values.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IEnumerable<ArchiveFile> AllXfbin() =>
        _archives.SelectMany(c => c.Entries.Where(e => e.Path.EndsWith(".xfbin", StringComparison.OrdinalIgnoreCase)).Select(e => new ArchiveFile(c, e)));

    static IEnumerable<string> ArchivePaths(string gameDir)
    {
        var cpks = Directory.GetFiles(System.IO.Path.Combine(gameDir, "data"), "*.cpk", SearchOption.AllDirectories).ToList();
        var dlc = System.IO.Path.Combine(gameDir, "dlc");
        if (Directory.Exists(dlc)) cpks.AddRange(Directory.GetFiles(dlc, "*.cpk", SearchOption.AllDirectories));
        return cpks.Where(f => !f.Contains("movie") && !f.Contains("adx2"))
                   .OrderBy(f => f.Contains("dlc") ? 2 : f.Contains("patch") ? 1 : 0)
                   .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);
    }
}
