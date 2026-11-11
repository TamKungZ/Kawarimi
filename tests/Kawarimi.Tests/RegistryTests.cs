using Kawarimi;

static class RegistryTests
{
    static SkinDefinition Skin(string code, string chr = "1nrt") => new() { CharacterCode = chr, SkinCode = code, ModelDirectory = "x" };

    public static void Run()
    {
        var reg = new SkinRegistry();
        int changes = 0; reg.Changed += () => changes++;
        var h1 = reg.Register(Skin("9n01"));
        reg.Register(Skin("9n02"));
        reg.Register(Skin("9s01", "1ssk"));
        T.Check("registry: multiple skins per character", reg.All.Count(s => s.CharacterCode == "1nrt") == 2);
        T.Check("registry: multiple characters at once", reg.All.Select(s => s.CharacterCode).Distinct().Count() == 2);
        T.Check("registry: registration order preserved", reg.All[0].SkinCode == "9n01" && reg.All[1].SkinCode == "9n02");
        T.Check("registry: Changed raised per registration", changes == 3);
        T.Check("registry: duplicate skin code rejected (case-insensitive)", T.Throws<InvalidOperationException>(() => reg.Register(Skin("9N01"))));
        T.Check("registry: too-long skin code rejected", T.Throws<ArgumentException>(() => reg.Register(Skin("12345678"))));
        T.Check("registry: non-alphanumeric skin code rejected", T.Throws<ArgumentException>(() => reg.Register(Skin("9n-1"))));
        T.Check("registry: character required", T.Throws<ArgumentException>(() => reg.Register(new SkinDefinition { SkinCode = "9x01", ModelDirectory = "x" })));
        T.Check("registry: negative character id rejected", T.Throws<ArgumentException>(() => reg.Register(new SkinDefinition { CharacterId = -3, SkinCode = "9x01", ModelDirectory = "x" })));
        T.Check("registry: unregister", reg.Unregister(h1) && reg.All.Count == 2 && !reg.Unregister(h1));
        T.Check("registry: numeric character id accepted", new SkinRegistry().Register(new SkinDefinition { CharacterId = 12, SkinCode = "9x01", ModelDirectory = "x" }).Id == 1);

        var r2 = new SkinRegistry();
        r2.Register(Skin("9n01") with { ModId = "alpha" });
        try { r2.Register(Skin("9n01")); T.Check("registry: conflict message names owner", false); }
        catch (InvalidOperationException e) { T.Check("registry: conflict message names owner", e.Message.Contains("alpha")); }
    }
}
