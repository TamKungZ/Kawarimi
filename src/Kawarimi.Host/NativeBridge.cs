using System.Text;

namespace Kawarimi.Host;

internal unsafe sealed class NativeBridge
{
    readonly NativeApi* _api;

    public NativeBridge(NativeApi* api) => _api = api;

    public string GameDirectory => new string(_api->GameDir);

    static byte[] Z(string? s) => Encoding.UTF8.GetBytes((s ?? "") + "\0");

    public void SetOption(string key, int value)
    {
        fixed (byte* p = Z(key)) _api->SetOption(p, value);
    }

    public void Log(string message)
    {
        fixed (byte* p = Z(message)) _api->Log(p);
    }

    public void SendNames(IReadOnlyDictionary<string, string> families)
    {
        if (_api->Version < 4) return;
        foreach (var (family, name) in families)
            fixed (byte* f = Z(family))
            fixed (byte* n = Z(name))
                _api->SetName(f, n);
    }

    public void Replace(IReadOnlyList<SkinDefinition> skins)
    {
        _api->Clear();
        foreach (var s in skins) Add(s);
        _api->Commit();
    }

    void Add(SkinDefinition s)
    {
        fixed (byte* code = Z(s.CharacterCode))
        fixed (byte* skin = Z(s.SkinCode))
        fixed (byte* dir = Z(Path.GetFullPath(s.ModelDirectory)))
        fixed (byte* replaces = Z(s.Replaces))
        {
            byte* character = s.CharacterCode is null ? null : code;
            int id = s.CharacterId ?? -1;
            if (_api->Version >= 3) _api->AddSkin2(character, id, skin, dir, string.IsNullOrEmpty(s.Replaces) ? null : replaces);
            else _api->AddSkin(character, id, skin, dir);
        }
    }
}
