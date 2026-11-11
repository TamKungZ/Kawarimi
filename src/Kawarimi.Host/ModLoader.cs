using System.Reflection;

namespace Kawarimi.Host;

public sealed class ModLoader
{
    readonly string _gameDir;
    readonly SkinRegistry _registry;
    readonly Action<string> _log;

    public ModLoader(string gameDir, SkinRegistry registry, Action<string> log)
    {
        _gameDir = gameDir;
        _registry = registry;
        _log = log;
    }

    public List<ModReportEntry> Report { get; } = new();

    public void LoadAll()
    {
        var mods = Path.Combine(_gameDir, "mods");
        if (!Directory.Exists(mods))
        {
            _log("no mods folder");
            return;
        }
        foreach (var dir in Directory.GetDirectories(mods).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            if (LegacyMod.IsContainer(dir))
                foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase)) LoadOne(sub, Path.GetFileName(dir) + "/");
            else LoadOne(dir);
        }
    }

    public void WriteReport(string path)
    {
        try { File.WriteAllText(path, ModReport.Format(Report, _registry.All.Count)); }
        catch { }
    }

    void LoadOne(string dir, string prefix = "")
    {
        var entry = new ModReportEntry { Folder = prefix + Path.GetFileName(dir), Id = Path.GetFileName(dir) };
        Report.Add(entry);
        var scope = new ModScope(_registry, entry.Id);
        try
        {
            LoadFolder(dir, entry, scope);
            entry.Skins = scope.Count;
            if (entry.Status != ModStatus.Skipped) entry.Status = ModStatus.Loaded;
        }
        catch (Exception e)
        {
            scope.Rollback();
            entry.Skins = 0;
            entry.Status = ModStatus.Failed;
            entry.Messages.Add(e is TargetInvocationException { InnerException: { } inner } ? inner.Message : e.Message);
        }
        foreach (var m in entry.Messages) _log($"mod [{entry.Folder}] {entry.Status}: {m}");
        if (entry.Messages.Count == 0) _log($"mod [{entry.Folder}] {entry.Status}: {entry.Skins} skin(s)");
    }

    void LoadFolder(string dir, ModReportEntry entry, ModScope scope)
    {
        var json = Path.Combine(dir, ModManifest.FileName);
        var dlls = Directory.GetFiles(dir, "*.dll");
        if (!File.Exists(json) && dlls.Length == 0 && LegacyMod.FilesFolder(dir) is { } legacyFiles)
        {
            LoadLegacy(dir, legacyFiles, entry, scope);
            return;
        }
        if (!File.Exists(json) && dlls.Length == 0)
        {
            entry.Status = ModStatus.Skipped;
            entry.Messages.Add("no skin.json or .dll found");
            return;
        }
        if (File.Exists(json)) LoadManifest(dir, json, entry, scope);
        foreach (var dll in dlls) LoadAssembly(dir, dll, entry, scope);
        scope.Warnings.ForEach(entry.Messages.Add);
    }

    static void LoadLegacy(string dir, string files, ModReportEntry entry, ModScope scope)
    {
        entry.Messages.Add("legacy mod (plain xfbin files, no skin.json)");
        foreach (var skin in LegacyMod.Skins(dir, files)) scope.Register(skin);
        scope.Warnings.ForEach(entry.Messages.Add);
    }

    static void LoadManifest(string dir, string json, ModReportEntry entry, ModScope scope)
    {
        var manifest = ModManifest.Parse(dir, File.ReadAllText(json));
        if (!string.IsNullOrEmpty(manifest.Id))
        {
            entry.Id = manifest.Id;
            scope.ModId = manifest.Id;
        }
        if (manifest.ApiVersion > KawarimiApi.Version)
            throw new InvalidOperationException($"needs Kawarimi API {manifest.ApiVersion}, installed is {KawarimiApi.Version}");
        foreach (var skin in manifest.Skins) scope.Register(skin);
    }

    void LoadAssembly(string dir, string dll, ModReportEntry entry, ModScope scope)
    {
        using var image = new MemoryStream(File.ReadAllBytes(dll));
        var asm = new ModAssemblyContext(dll).LoadFromStream(image);
        foreach (var type in asm.GetTypes().Where(t => typeof(IKawarimiMod).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var mod = (IKawarimiMod)Activator.CreateInstance(type)!;
            mod.Load(new ModContext(_gameDir, dir, scope, entry.Messages.Add));
            _log($"mod [{entry.Folder}] loaded {type.FullName}");
        }
    }
}
