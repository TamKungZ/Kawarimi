using Kawarimi.Tools.Commands;

namespace Kawarimi.Tools.Cli;

internal static class Menu
{
    static readonly (string Label, Func<int> Run)[] Items =
    [
        ("Create a new mod project (pick a character: model, textures and C# code go to projects\\)", () => ModCommands.New([])),
        ("Build a project into a mod (projects\\ -> mods\\)", () => ModCommands.Build([])),
        ("Find a character / costume code", () => CharacterCommands.List(Ui.Ask("Name or code (Enter = all)", ""))),
        ("List projects and mods", ModCommands.List),
        ("Show all commands", () =>
        {
            CommandLine.PrintUsage();
            return 0;
        }),
    ];

    public static int Run()
    {
        while (true)
        {
            Console.WriteLine();
            Ui.Title("Kawarimi");
            Ui.Info("In game: hover a costume in character select, F7 = next mod for it, F8 = original.");
            int pick = Ui.Choose("Choose (Enter = quit)", Items.Select(i => i.Label).ToList());
            if (pick < 0) return 0;
            try
            {
                Items[pick].Run();
            }
            catch (Exception e)
            {
                Ui.Error("Error: " + e.Message);
            }
        }
    }
}
