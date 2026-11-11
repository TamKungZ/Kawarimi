using System.Runtime.InteropServices;

namespace Kawarimi.Host;

public static unsafe class Entry
{
    static readonly object Gate = new();
    static NativeBridge? _bridge;
    static ModWatcher? _watcher;
    internal static SkinRegistry Registry { get; } = new();

    [UnmanagedCallersOnly]
    public static int Init(NativeApi* api)
    {
        try
        {
            _bridge = new NativeBridge(api);
            Action<string> log = _bridge.Log;
            string gameDir = _bridge.GameDirectory, kdir = Path.Combine(gameDir, "kawarimi");

            var config = HostConfig.Load(Path.Combine(kdir, "config.json"), log);
            _bridge.SetOption("strategy", config.StrategyValue);
            _bridge.SetOption("trace", config.Trace ? 1 : 0);
            _bridge.SetOption("online", config.OnlineValue);
            _bridge.SendNames(CharacterNames.Families);

            LoadMods(gameDir, kdir, log);
            log($"Kawarimi ready, {Registry.All.Count} skin(s), strategy={config.Strategy}");
            _watcher = new ModWatcher(Path.Combine(gameDir, "mods"), () =>
            {
                LoadMods(gameDir, kdir, log);
                log($"mods reloaded: {Registry.All.Count} skin(s)");
            });
            return 0;
        }
        catch (Exception e)
        {
            try { _bridge?.Log("Init failed: " + e); } catch { }
            return 1;
        }
    }

    static void LoadMods(string gameDir, string kdir, Action<string> log)
    {
        lock (Gate)
        {
            Registry.Clear();
            var loader = new ModLoader(gameDir, Registry, log);
            loader.LoadAll();
            loader.WriteReport(Path.Combine(kdir, "mods-report.txt"));
            _bridge!.Replace(Registry.All);
        }
    }
}
