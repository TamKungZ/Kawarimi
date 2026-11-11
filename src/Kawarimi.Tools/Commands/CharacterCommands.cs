using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class CharacterCommands
{
    public static int List(string search)
    {
        var catalog = Load(GameFolder.Find());
        var found = catalog.Search(search).ToList();
        foreach (var c in found) Ui.Info($"  {c.Label,-28} costumes: {string.Join(", ", c.Costumes)}");
        Ui.Info($"{found.Count} character(s)");
        return found.Count > 0 ? 0 : 1;
    }

    public static CharacterCatalog Load(string gameDir)
    {
        var catalog = CharacterCatalog.Load(gameDir, () => new GameArchives(gameDir));
        if (!catalog.FromGame)
            Ui.Warn("start the game once with Kawarimi installed to get the full list (kawarimi\\characters.json); showing model codes from the archives");
        return catalog;
    }

    public static string? PickCostume(CharacterCatalog catalog)
    {
        Ui.Info("Tip: in character select the Kawarimi console shows 'COSTUME <code>' for the costume under the cursor.");
        while (true)
        {
            var query = Ui.Ask("Character name or costume code (Enter = cancel)", "");
            if (query.Length == 0) return null;
            if (catalog.OwnerOf(query) != null) return query;
            var matches = catalog.Search(query).Take(40).ToList();
            if (matches.Count == 0)
            {
                Ui.Error("  nothing found, try another name or code");
                continue;
            }
            int c = matches.Count == 1 ? 0 : Ui.Choose("Character", matches.Select(m => m.Label).ToList());
            if (c < 0) continue;
            var costumes = matches[c].Costumes;
            if (costumes.Count == 1) return costumes[0];
            int k = Ui.Choose("Costume", costumes.Select((x, i) => $"costume {i + 1}: {x}").ToList());
            if (k >= 0) return costumes[k];
        }
    }
}
