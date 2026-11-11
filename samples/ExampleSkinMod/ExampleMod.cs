using Kawarimi;

namespace ExampleSkinMod;

public sealed class ExampleMod : IKawarimiMod
{
    public void Load(IKawarimiContext context)
    {
        string files = Path.Combine(context.ModDirectory, "files");

        context.Skins.Register(new SkinDefinition
        {
            CharacterCode = "1nrt",
            SkinCode = "9n01",
            DisplayName = "Example Naruto A",
            ModelDirectory = files,
        });

        context.Skins.Register(new SkinDefinition
        {
            CharacterCode = "2hnt",
            SkinCode = "9h01",
            Replaces = "2hnt",
            DisplayName = "Example Hinata recolour",
            ModelDirectory = files,
        });

        context.Log("ExampleSkinMod registered 2 skins");
    }
}
