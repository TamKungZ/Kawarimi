# C# API

Reference `<game>\kawarimi\Kawarimi.Api.dll` with `<Private>false</Private>` (the host supplies it). `Kawarimi new` creates a ready project.

```csharp
public sealed class Mod : IKawarimiMod
{
    public void Load(IKawarimiContext context) =>
        context.Skins.Register(new SkinDefinition
        {
            CharacterCode = "2ino",
            SkinCode = "r1ab",
            Replaces = "2ino",
            DisplayName = "InoRed",
            ModelDirectory = Path.Combine(context.ModDirectory, "files"),
        });
}
```

* `Replaces` set: the skin serves that costume's files while it is switched on with F7.
  Not set: `SkinCode` is a new costume code with its own complete file set.
* `SkinCode` must be unique across all mods. A mod whose `Load` throws, or that registers an invalid or duplicate skin, is rolled back
  as a whole; other mods still load.
* Mods load alphabetically by folder and are reloaded when `mods\` changes.

## `kawarimi\config.json`

| key | default | |
|---|---|---|
| `console` | `true` | log window next to the game |
| `trace` | `false` | log every costume lookup with its caller |
| `strategy` | `lookup` | `record` also writes skin codes into free in-memory costume slots |
| `online` | `visual` | after a remote connection: `visual` serves only model and texture files (`bod*`, `acc*`) of skins that replace a costume and switches off new-code skins, `off` switches off all skins, `all` changes nothing |
