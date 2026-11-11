using Kawarimi.Tools.Capture;
using Kawarimi.Tools.Commands;

namespace Kawarimi.Tools.Cli;

internal sealed record Command(string Name, string Args, string Help, int MinArgs, int MaxArgs, Func<string[], int> Run);

internal static class CommandLine
{
    public static readonly Command[] Commands =
    [
        new("characters", "[search]", "list characters and their costume codes (search by name or code)", 0, 1, a => CharacterCommands.List(a.ElementAtOrDefault(0) ?? "")),
        new("new", "[costume] [Name]", "create projects\\<Name>: model (OBJ), textures (PNG) and a C# project for one costume", 0, 2, ModCommands.New),
        new("build", "[Name] [--zip] [--all]", "build projects\\<Name> into mods\\<Name> (a running game reloads it); --zip also writes share\\<Name>.zip to hand out; --all builds every project", 0, 3, ModCommands.Build),
        new("path add", "", "put Kawarimi on your PATH so  Kawarimi build <Name>  works from any folder", 0, 0, _ => PathCommands.Add()),
        new("path remove", "", "take Kawarimi off your PATH", 0, 0, _ => PathCommands.Remove()),
        new("list", "", "list projects and mods", 0, 0, _ => ModCommands.List()),
        new("extract", "<filter> [outDir]", "decrypt + decompress game archive files whose path contains <filter>", 1, 2, a => ArchiveCommands.Extract(a[0], a.ElementAtOrDefault(1) ?? "kawarimi-extract")),
        new("encode", "<in.xfbin> <out>", "encrypt a plain xfbin into the archive form", 2, 2, a => ArchiveCommands.Encode(a[0], a[1])),
        new("index", "[filter]", "list xfbin files inside the game archives", 0, 1, a => ArchiveCommands.Index(a.ElementAtOrDefault(0) ?? "")),
        new("info", "<file.xfbin>", "show the chunks of a plain xfbin", 1, 1, a => ContainerCommands.Info(a[0])),
        new("unpack", "<file.xfbin> <dir>", "split an xfbin into chunk files", 2, 2, a => ContainerCommands.Unpack(a[0], a[1])),
        new("pack", "<dir> <out.xfbin>", "rebuild an xfbin from an unpacked folder", 2, 2, a => ContainerCommands.Pack(a[0], a[1])),
        new("retarget", "<in> <old> <new> <out>", "same-length re-code of one file (e.g. 1nrt -> 9k01)", 4, 4, a => ContainerCommands.Retarget(a[0], a[1], a[2], a[3])),
        new("textures export", "<file.xfbin> <dir>", "textures -> PNG", 2, 2, a => AssetCommands.ExportTextures(a[0], a[1])),
        new("textures import", "<file.xfbin> <dir> <out.xfbin>", "edited PNGs (keep the tex_<chunk>_<index>_ name start) -> new xfbin", 3, 3, a => AssetCommands.ImportTextures(a[0], a[1], a[2])),
        new("model export", "<file.xfbin> <dir>", "meshes -> OBJ (opens in any 3D program)", 2, 2, a => AssetCommands.ExportModel(a[0], a[1])),
        new("model import", "<file.xfbin> <edited.obj> <out.xfbin> [--normals]", "edited OBJ -> new xfbin (changed parts are rebuilt)", 3, 4,
            a => AssetCommands.ImportModel(a[0], a[1], a[2], a.ElementAtOrDefault(3) == "--normals")),
        new("skin compose", "<extractDir> <gameplayCode> <modelCode|-> <newCode> <character> <name> <modDir>", "build a new-code skin from extracted files", 7, 7,
            a => SkinCommands.Compose(a[0], a[1], a[2], a[3], a[4], a[5], a[6])),
        new("skin compare", "<extractDir> <codeA> <codeB>", "compare the bone/mesh name tables of two models", 3, 3, a => SkinCommands.Compare(a[0], a[1], a[2])),
        new("skin verify", "<modDir>", "check a mod folder the way Kawarimi loads it", 1, 1, a => SkinCommands.Verify(a[0])),
        new("capture", "[outDir] [seconds]", "save decrypted files from the RUNNING game (research)", 0, 2,
            a => CaptureCommand.Run(GameFolder.Find(), a.ElementAtOrDefault(0) ?? "kawarimi-capture", a.Length > 1 ? int.Parse(a[1]) : 180)),
        new("capture name", "<captureDir> <outDir>", "name files dumped by the in-game capture (kawarimi\\capture)", 2, 2, a => CaptureCommand.NameDir(GameFolder.Find(), a[0], a[1])),
    ];

    public static int Run(string[] args)
    {
        if (args.Length == 0 || args[0] is "help" or "-h" or "--help" or "/?")
        {
            PrintUsage();
            return 0;
        }
        var cmd = Commands.Where(c => Matches(c, args)).OrderByDescending(c => c.Name.Length).FirstOrDefault();
        if (cmd == null)
        {
            Ui.Error($"Unknown command: {string.Join(' ', args)}");
            PrintUsage();
            return 2;
        }
        var rest = args.Skip(cmd.Name.Split(' ').Length).ToArray();
        if (rest.Length < cmd.MinArgs || rest.Length > cmd.MaxArgs)
        {
            Ui.Error($"Usage: Kawarimi {cmd.Name} {cmd.Args}");
            return 2;
        }
        try
        {
            return cmd.Run(rest);
        }
        catch (Exception e)
        {
            Ui.Error("Error: " + e.Message);
            return 1;
        }
    }

    public static void PrintUsage()
    {
        Ui.Title("Kawarimi - skin mods for NARUTO SHIPPUDEN: Ultimate Ninja STORM 4");
        Ui.Info("Run without arguments for the menu. Commands:");
        foreach (var c in Commands)
        {
            Ui.Info($"  {c.Name} {c.Args}");
            Ui.Info($"      {c.Help}");
        }
    }

    static bool Matches(Command c, string[] args)
    {
        var words = c.Name.Split(' ');
        return args.Length >= words.Length && words.Select((w, i) => args[i].Equals(w, StringComparison.OrdinalIgnoreCase)).All(x => x);
    }
}
