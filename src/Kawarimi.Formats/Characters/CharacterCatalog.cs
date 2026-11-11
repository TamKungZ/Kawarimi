using System.Text.Json;

namespace Kawarimi.Formats;

public sealed record CharacterEntry(int Id, string Code, IReadOnlyList<string> Costumes)
{
    public string? Name => CharacterNames.For(Code);
    public string Label => Name is { } n ? $"{n} [{Code}]" : $"[{Code}]";
}

public sealed class CharacterCatalog
{
    public const string FileName = "characters.json";

    public CharacterCatalog(IReadOnlyList<CharacterEntry> characters, bool fromGame)
    {
        Characters = characters;
        FromGame = fromGame;
    }

    public IReadOnlyList<CharacterEntry> Characters { get; }
    public bool FromGame { get; }

    public static CharacterCatalog Load(string gameDir, Func<GameArchives>? archives = null)
    {
        var path = Path.Combine(gameDir, "kawarimi", FileName);
        if (File.Exists(path)) return new CharacterCatalog(Parse(File.ReadAllText(path)), true);
        return new CharacterCatalog(archives == null ? [] : FromArchives(archives()), false);
    }

    public static List<CharacterEntry> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        return doc.RootElement.EnumerateArray()
            .Select(e => new CharacterEntry(
                e.GetProperty("id").GetInt32(),
                e.GetProperty("code").GetString() ?? "",
                e.GetProperty("costumes").EnumerateArray().Select(c => c.GetString() ?? "").Where(c => c.Length > 0).ToList()))
            .ToList();
    }

    public IEnumerable<CharacterEntry> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Characters;
        return Characters.Where(c =>
            (c.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            c.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            c.Costumes.Any(x => x.Equals(query, StringComparison.OrdinalIgnoreCase)));
    }

    public CharacterEntry? OwnerOf(string costume) =>
        Characters.FirstOrDefault(c => c.Costumes.Any(x => x.Equals(costume, StringComparison.OrdinalIgnoreCase)));

    static List<CharacterEntry> FromArchives(GameArchives archives)
    {
        var codes = archives.Find("data/spc/")
            .Select(f => System.IO.Path.GetFileName(f.Path))
            .Where(n => n.EndsWith("bod1.xfbin", StringComparison.OrdinalIgnoreCase) && n.Length is >= 13 and <= 17)
            .Select(n => n[..^10])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase);
        return codes.Select((c, i) => new CharacterEntry(i, c, [c])).ToList();
    }
}
