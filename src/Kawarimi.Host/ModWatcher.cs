namespace Kawarimi.Host;

internal sealed class ModWatcher : IDisposable
{
    readonly FileSystemWatcher _watcher;
    readonly Timer _debounce;
    readonly Action _reload;

    public ModWatcher(string modsDir, Action reload)
    {
        _reload = reload;
        Directory.CreateDirectory(modsDir);
        _debounce = new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(modsDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
        _watcher.Deleted += (_, _) => Schedule();
        _watcher.Renamed += (_, _) => Schedule();
        _watcher.EnableRaisingEvents = true;
    }

    void Schedule() => _debounce.Change(1500, Timeout.Infinite);

    void Fire()
    {
        try { _reload(); }
        catch (Exception) { }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
