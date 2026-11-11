using System.Text.Json;

namespace Kawarimi.Host;

public sealed record ModManifest(string Id, string Name, string Version, int ApiVersion, IReadOnlyList<SkinDefinition> Skins)
{
    public const string FileName = "skin.json";

    public static ModManifest Parse(string modDir, string jsonText)
    {
        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;
        var items = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Array) items.AddRange(root.EnumerateArray());
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("skins", out var arr)) items.AddRange(arr.EnumerateArray());
        else if (root.ValueKind == JsonValueKind.Object) items.Add(root);
        else throw new InvalidDataException("skin.json must be an object or an array");

        var skins = items.Select(it => ParseSkin(modDir, it)).ToList();
        return new ModManifest(Str(root, "id"), Str(root, "name"), Str(root, "version"), Int(root, "apiVersion") ?? 1, skins);
    }

    static SkinDefinition ParseSkin(string modDir, JsonElement it)
    {
        if (it.ValueKind != JsonValueKind.Object) throw new InvalidDataException("each skin must be an object");
        var code = Str(it, "skinCode");
        if (code.Length == 0) throw new InvalidDataException("skinCode missing");
        string files = Str(it, "files") is { Length: > 0 } f ? f : "files";
        return new SkinDefinition
        {
            CharacterCode = Str(it, "character") is { Length: > 0 } c ? c : null,
            CharacterId = Int(it, "characterId"),
            SkinCode = code,
            DisplayName = Str(it, "name"),
            Replaces = Str(it, "replaces") is { Length: > 0 } rp ? rp : null,
            ModelDirectory = Path.GetFullPath(Path.Combine(modDir, files)),
        };
    }

    static string Str(JsonElement e, string n) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static int? Int(JsonElement e, string n) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
