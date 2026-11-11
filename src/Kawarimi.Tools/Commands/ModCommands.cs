using Kawarimi.Formats;
using Kawarimi.Host;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class ModCommands
{
    public static int New(string[] args)
    {
        string game = GameFolder.Find();
        var catalog = CharacterCommands.Load(game);
        string? costume = args.ElementAtOrDefault(0) ?? CharacterCommands.PickCostume(catalog);
        if (string.IsNullOrEmpty(costume)) return 1;
        string character = catalog.OwnerOf(costume)?.Code ?? costume;
        string name = Safe(args.ElementAtOrDefault(1) ?? Ui.Ask("Mod name", $"My_{costume}"));
        string projectDir = Path.Combine(GameFolder.ProjectsFolder(game), name);
        if (Directory.Exists(projectDir) && Directory.EnumerateFileSystemEntries(projectDir).Any())
            throw new InvalidOperationException($"{projectDir} already exists. Pick another name.");

        Ui.Info($"Reading costume {costume} ({CharacterNames.For(character) ?? character}) from the game archives...");
        var info = new ModProjectInfo(name, costume, character, ModProject.SkinCodeFor(name));
        foreach (var f in ModProject.Create(new GameArchives(game), projectDir, info))
        {
            if (f.Problem != null) Ui.Warn($"{f.Name}: cannot be edited ({f.Problem})");
            else Ui.Info($"  {f.Name}: {f.Textures} texture(s), {f.Meshes} mesh group(s)");
        }
        Ui.Ok($"Project created: {projectDir}");
        Ui.Info($"  edit\\<file>\\textures\\*.png   textures (any size, keep the tex_<chunk>_<index>_ name start)");
        Ui.Info($"  edit\\<file>\\model\\*.obj      model for any 3D program (meters, Y up; keep the object names)");
        Ui.Info($"  {name}.csproj + Mod.cs        the mod's C# code (optional to change)");
        Ui.Info($"Next: edit, then  Kawarimi build {name}");
        return 0;
    }

    public static int Build(string[] args)
    {
        string game = GameFolder.Find();
        bool zip = args.Contains("--zip", StringComparer.OrdinalIgnoreCase), all = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        if (args.FirstOrDefault(a => a.StartsWith("--") && !a.Equals("--zip", StringComparison.OrdinalIgnoreCase) && !a.Equals("--all", StringComparison.OrdinalIgnoreCase)) is { } unknown)
            throw new InvalidOperationException($"Unknown option {unknown}. Options: --zip, --all.");
        var names = args.Where(a => !a.StartsWith("--")).ToList();
        if (all) names = Folders(GameFolder.ProjectsFolder(game)).Where(d => File.Exists(Path.Combine(d, ModProject.InfoFile))).Select(d => Path.GetFileName(d)!).ToList();
        else if (names.Count == 0 && PickProject(game) is { Length: > 0 } picked) names.Add(picked);
        if (names.Count == 0) return 1;
        int failed = names.Count(name => TryBuildProject(game, name, zip) != 0);
        if (names.Count > 1) Ui.Info($"{names.Count - failed} of {names.Count} project(s) built.");
        return failed == 0 ? 0 : 1;
    }

    static int TryBuildProject(string game, string name, bool zip)
    {
        try
        {
            return BuildProject(game, name, zip);
        }
        catch (Exception e)
        {
            Ui.Error($"{name}: {e.Message}");
            return 1;
        }
    }

    static int BuildProject(string game, string name, bool zip)
    {
        string projectDir = Directory.Exists(name) ? Path.GetFullPath(name) : Path.Combine(GameFolder.ProjectsFolder(game), name);
        var info = ModProject.ReadInfo(projectDir);
        string modDir = Path.Combine(GameFolder.ModsFolder(game), info.Name);
        Directory.CreateDirectory(modDir);

        var built = ModProject.BuildFiles(projectDir, modDir);
        foreach (var b in built)
        {
            if (b.Note != null) Ui.Info($"  {b.Name}: {b.Note}");
            if (b.Problem != null) Ui.Warn($"{b.Name}: {b.Problem}");
            else if (b.Changed) Ui.Ok($"  {b.Name}: {b.Textures} texture(s), {b.Meshes} mesh group(s) changed");
            else Ui.Info($"  {b.Name}: unchanged");
        }
        if (!built.Any(b => b.Changed)) Ui.Warn("nothing changed yet - edit the PNG/OBJ files in the project first");

        if (ModCompiler.HasSdk())
        {
            if (!ModCompiler.Compile(projectDir, info, modDir)) return 1;
            File.Delete(Path.Combine(modDir, ModManifest.FileName));
            Ui.Ok($"  {info.Name}.dll compiled");
        }
        else
        {
            ModProject.WriteManifest(modDir, info);
            File.Delete(Path.Combine(modDir, info.Name + ".dll"));
            Ui.Info("  .NET SDK not installed: Mod.cs skipped, skin.json written instead");
        }
        Ui.Ok($"Mod ready: {modDir}");
        if (zip)
        {
            Ui.Ok($"Zip ready: {ModPackage.Zip(modDir, GameFolder.ShareFolder(game))}");
            Ui.Info("  Others extract it into their <game>\\mods\\ folder.");
        }
        Ui.Info($"In game (character select): hover {info.Costume}, press F7. A running game picks the change up within a few seconds.");
        return 0;
    }

    public static int List()
    {
        string game = GameFolder.Find();
        Ui.Title("Projects (projects\\, your work)");
        foreach (var dir in Folders(GameFolder.ProjectsFolder(game)))
        {
            try
            {
                var i = ModProject.ReadInfo(dir);
                Ui.Info($"  {i.Name,-24} replaces {i.Costume} ({CharacterNames.For(i.Character) ?? i.Character})");
            }
            catch (Exception)
            {
                Ui.Info($"  {Path.GetFileName(dir),-24} (not a Kawarimi project)");
            }
        }
        Ui.Title("Mods (mods\\, loaded by the game)");
        foreach (var dir in Folders(GameFolder.ModsFolder(game)))
        {
            var json = Path.Combine(dir, ModManifest.FileName);
            string kind = File.Exists(json) ? "skin.json" : Directory.GetFiles(dir, "*.dll").Length > 0 ? "C# mod" : "empty";
            Ui.Info($"  {Path.GetFileName(dir),-24} {kind}");
        }
        return 0;
    }

    static IEnumerable<string> Folders(string dir) =>
        Directory.Exists(dir) ? Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase) : [];

    static string? PickProject(string game)
    {
        var projects = Folders(GameFolder.ProjectsFolder(game)).Where(d => File.Exists(Path.Combine(d, ModProject.InfoFile))).Select(Path.GetFileName).ToList();
        if (projects.Count == 0)
        {
            Ui.Error("No project found. Create one first (menu 1 or: Kawarimi new).");
            return null;
        }
        int i = Ui.Choose("Project to build", projects!);
        return i < 0 ? null : projects[i];
    }

    static string Safe(string name) => new(name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
}
