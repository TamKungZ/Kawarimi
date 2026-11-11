using Kawarimi;
using Kawarimi.Host;

static class ModLoaderTests
{
    static string NewGame()
    {
        var d = Path.Combine(Path.GetTempPath(), "kw-mods-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(d, "mods"));
        return d;
    }

    static string Mod(string game, string name, string json, params string[] files)
    {
        var dir = Path.Combine(game, "mods", name);
        Directory.CreateDirectory(Path.Combine(dir, "files"));
        File.WriteAllText(Path.Combine(dir, "skin.json"), json);
        foreach (var f in files) File.WriteAllText(Path.Combine(dir, "files", f), "x");
        return dir;
    }

    public static void Run()
    {
        var one = ModManifest.Parse(@"C:\mods\A", """{ "character":"1nrt", "skinCode":"9n01", "name":"N" }""");
        T.Check("manifest: single object", one.Skins.Count == 1 && one.Skins[0].DisplayName == "N");
        T.Check("manifest: default files folder", one.Skins[0].ModelDirectory.EndsWith(@"A\files"));
        var many = ModManifest.Parse(@"C:\mods\A", """{ "id":"my.mod","version":"1.2","apiVersion":1,"skins":[ {"characterId":3,"skinCode":"a1","files":"f"}, {"character":"1nrt","skinCode":"a2"} ] }""");
        T.Check("manifest: id, version, api version", many.Id == "my.mod" && many.Version == "1.2" && many.ApiVersion == 1);
        T.Check("manifest: skins array with id + custom folder", many.Skins.Count == 2 && many.Skins[0].CharacterId == 3 && many.Skins[0].ModelDirectory.EndsWith(@"A\f"));
        T.Check("manifest: bare array accepted", ModManifest.Parse("x", """[ {"character":"1nrt","skinCode":"b1"} ]""").Skins.Count == 1);
        T.Check("manifest: skinCode required", T.Throws<InvalidDataException>(() => ModManifest.Parse("x", """{ "character":"1nrt" }""")));
        T.Check("manifest: malformed json rejected", T.Throws<System.Text.Json.JsonException>(() => ModManifest.Parse("x", "{ nope")));

        var cfg = HostConfig.Parse("{ \"strategy\":\"record\", \"trace\":true, \"online\":\"off\", // comment\n }");
        T.Check("config: parses strategy/trace/online (comments + trailing commas ok)", cfg.StrategyValue == 1 && cfg.Trace && cfg.OnlineValue == 0);
        T.Check("config: defaults", HostConfig.Parse("{}") is { StrategyValue: 0, Trace: false, OnlineValue: 1 });

        var game = NewGame();
        try
        {
            Mod(game, "b_second", """{ "id":"second","skins":[{"character":"1kak","skinCode":"9k01"}] }""", "9k01bod1.xfbin");
            Mod(game, "a_first", """{ "id":"first","skins":[{"character":"1nrt","skinCode":"9n01"},{"character":"1nrt","skinCode":"9n02"}] }""", "9n01bod1.xfbin", "9n02bod1.xfbin");
            Mod(game, "c_conflict", """{ "id":"third","skins":[{"character":"1ssk","skinCode":"9s01"},{"character":"1ssk","skinCode":"9N01"}] }""", "9s01bod1.xfbin");
            Mod(game, "d_missing_folder", """{ "skins":[{"character":"1ssk","skinCode":"9s02","files":"nope"}] }""");
            Mod(game, "e_badjson", "{ not json");
            Mod(game, "f_futureapi", """{ "apiVersion": 99, "skins":[{"character":"1ssk","skinCode":"9s03"}] }""", "9s03bod1.xfbin");
            Mod(game, "g_nomodel", """{ "skins":[{"character":"1ssk","skinCode":"9s04"}] }""");
            Directory.CreateDirectory(Path.Combine(game, "mods", "h_empty"));

            var reg = new SkinRegistry();
            var loader = new ModLoader(game, reg, _ => { });
            loader.LoadAll();
            var by = loader.Report.ToDictionary(r => r.Folder);

            T.Check("loader: mods load in alphabetical order", reg.All.Select(s => s.SkinCode).Take(2).SequenceEqual(["9n01", "9n02"]));
            T.Check("loader: skins for several characters from several mods", reg.All.Select(s => s.CharacterCode).Distinct().Count() >= 2);
            T.Check("loader: skins tagged with manifest mod id", reg.All.First(s => s.SkinCode == "9n01").ModId == "first");
            T.Check("loader: conflicting mod fails as a whole", by["c_conflict"].Status == ModStatus.Failed && by["c_conflict"].Messages[0].Contains("first"));
            T.Check("loader: failed mod is rolled back (its valid skin is gone)", !reg.All.Any(s => s.SkinCode == "9s01"));
            T.Check("loader: earlier mods unaffected by a later conflict", reg.All.Any(s => s.SkinCode == "9n01") && reg.All.Any(s => s.SkinCode == "9k01"));
            T.Check("loader: missing folder reported", by["d_missing_folder"].Status == ModStatus.Failed && by["d_missing_folder"].Messages[0].Contains("not found"));
            T.Check("loader: bad json fails only that mod", by["e_badjson"].Status == ModStatus.Failed && by["a_first"].Status == ModStatus.Loaded);
            T.Check("loader: newer API version refused", by["f_futureapi"].Status == ModStatus.Failed && !reg.All.Any(s => s.SkinCode == "9s03"));
            T.Check("loader: missing model file is a warning, skin still registered", by["g_nomodel"].Status == ModStatus.Loaded && by["g_nomodel"].Messages.Any(m => m.Contains("bod1.xfbin")));
            T.Check("loader: empty folder skipped", by["h_empty"].Status == ModStatus.Skipped);

            var report = Path.Combine(game, "report.txt");
            loader.WriteReport(report);
            var text = File.ReadAllText(report);
            T.Check("loader: report lists every mod", by.Keys.All(text.Contains) && text.Contains("[Failed]") && text.Contains("[Loaded]"));
        }
        finally { try { Directory.Delete(game, true); } catch { } }
    }
}
