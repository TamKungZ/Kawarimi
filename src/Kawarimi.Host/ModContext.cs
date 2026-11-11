namespace Kawarimi.Host;

internal sealed class ModContext : IKawarimiContext
{
    readonly Action<string> _log;

    public ModContext(string game, string mod, ISkinRegistry skins, Action<string> log)
    {
        GameDirectory = game;
        ModDirectory = mod;
        Skins = skins;
        _log = log;
    }

    public string GameDirectory { get; }
    public string ModDirectory { get; }
    public ISkinRegistry Skins { get; }
    public void Log(string message) => _log(message);
}
