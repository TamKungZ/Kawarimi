using System.Text.Json;

namespace Kawarimi.Formats;

public sealed record ModProjectInfo(string Name, string Costume, string Character, string SkinCode);

public static class ModProject
{
    public const string InfoFile = "kawarimi.project.json";
    public const string OriginalFolder = "original";
    public const string EditFolder = "edit";
    public const string FilesFolder = "files";
    public const string ManifestFile = "skin.json";

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public sealed record CreatedFile(string Name, int Textures, int Meshes, string? Problem);
    public sealed record BuiltFile(string Name, int Textures, int Meshes, bool Changed, string? Problem, string? Note = null);

    public static List<CreatedFile> Create(GameArchives archives, string projectDir, ModProjectInfo info)
    {
        var sources = archives.Find($"data/spc/{info.Costume}")
            .Where(f => IsModelFile(Path.GetFileName(f.Path), info.Costume))
            .ToList();
        if (sources.Count == 0) throw new InvalidOperationException($"No model files for costume [{info.Costume}] in the game archives.");

        string original = Path.Combine(projectDir, OriginalFolder), edit = Path.Combine(projectDir, EditFolder);
        Directory.CreateDirectory(original);
        var created = new List<CreatedFile>();
        foreach (var src in sources)
        {
            string name = Path.GetFileName(src.Path);
            var data = src.ReadPlain();
            var result = Unpack(data, name, Path.Combine(edit, Stem(name)));
            if (result.Problem == null) File.WriteAllBytes(Path.Combine(original, name), data);
            created.Add(result);
        }
        File.WriteAllText(Path.Combine(projectDir, InfoFile), JsonSerializer.Serialize(info, Json));
        File.WriteAllText(Path.Combine(projectDir, info.Name + ".csproj"), ModTemplates.Csproj(info.Name));
        File.WriteAllText(Path.Combine(projectDir, "Mod.cs"), ModTemplates.ModClass(info));
        return created;
    }

    public static ModProjectInfo ReadInfo(string projectDir)
    {
        var path = Path.Combine(projectDir, InfoFile);
        if (!File.Exists(path)) throw new FileNotFoundException($"{projectDir} is not a Kawarimi project ({InfoFile} missing).");
        return JsonSerializer.Deserialize<ModProjectInfo>(File.ReadAllText(path), Json) ?? throw new InvalidDataException(InfoFile);
    }

    public static List<BuiltFile> BuildFiles(string projectDir, string modDir)
    {
        string original = Path.Combine(projectDir, OriginalFolder), edit = Path.Combine(projectDir, EditFolder), files = Path.Combine(modDir, FilesFolder);
        Directory.CreateDirectory(files);
        var built = new List<BuiltFile>();
        foreach (var path in Directory.GetFiles(original, "*.xfbin").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileName(path), work = Path.Combine(edit, Stem(name)), output = Path.Combine(files, name);
            built.Add(BuildOne(path, name, work, output));
        }
        foreach (var stale in Directory.GetFiles(files).Where(f => !built.Any(b => b.Changed && b.Name.Equals(Path.GetFileName(f), StringComparison.OrdinalIgnoreCase))))
            File.Delete(stale);
        return built;
    }

    public static void WriteManifest(string modDir, ModProjectInfo info)
    {
        var manifest = new
        {
            id = info.Name,
            name = info.Name,
            version = "1.0",
            apiVersion = KawarimiApi.Version,
            skins = new[] { new { character = info.Character, skinCode = info.SkinCode, replaces = info.Costume, name = info.Name, files = FilesFolder } },
        };
        File.WriteAllText(Path.Combine(modDir, ManifestFile), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string SkinCodeFor(string name)
    {
        uint h = 2166136261;
        foreach (char c in name.ToLowerInvariant()) h = (h ^ c) * 16777619;
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        return "r" + digits[(int)(h % 36)] + digits[(int)(h / 36 % 36)] + digits[(int)(h / 1296 % 36)];
    }

    static BuiltFile BuildOne(string path, string name, string work, string output)
    {
        var data = File.ReadAllBytes(path);
        try
        {
            var x = XfbinFile.Parse(data);
            string textures = Path.Combine(work, "textures"), obj = Path.Combine(work, "model", Stem(name) + ".obj");
            int tex = Directory.Exists(textures) ? TextureFolder.Import(x, textures).Count : 0;
            var import = File.Exists(obj) ? ModelFolder.Import(x, obj, normals: false) : null;
            int meshes = import?.Changed.Count ?? 0;
            var bytes = x.ToBytes();
            bool changed = !bytes.AsSpan().SequenceEqual(data);
            if (changed) File.WriteAllBytes(output, bytes);
            return new BuiltFile(name, tex, meshes, changed, null, Describe(import));
        }
        catch (Exception e)
        {
            return new BuiltFile(name, 0, 0, false, e.Message);
        }
    }

    public static string? Describe(MeshImportResult? r)
    {
        if (r == null) return null;
        var notes = new List<string>();
        if (r.Rebuilt.Count > 0) notes.Add($"rebuilt {string.Join(", ", r.Rebuilt)}");
        if (r.Removed.Count > 0) notes.Add($"removed {string.Join(", ", r.Removed)}");
        notes.AddRange(r.Added.Select(a => $"added {a.Name} into {a.Into}"));
        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    static CreatedFile Unpack(byte[] data, string name, string work)
    {
        try
        {
            var x = XfbinFile.Parse(data);
            int tex = TextureFolder.Export(x, Path.Combine(work, "textures")).Written.Count;
            var (_, polys, _) = ModelFolder.Export(x, name, Path.Combine(work, "model"), "../textures");
            return new CreatedFile(name, tex, polys.Count, null);
        }
        catch (Exception e)
        {
            return new CreatedFile(name, 0, 0, e.Message);
        }
    }

    static bool IsModelFile(string fileName, string code)
    {
        if (!fileName.StartsWith(code, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = fileName[code.Length..];
        return rest.StartsWith("bod", StringComparison.OrdinalIgnoreCase) || rest.StartsWith("acc", StringComparison.OrdinalIgnoreCase);
    }

    static string Stem(string name) => name.EndsWith(".xfbin", StringComparison.OrdinalIgnoreCase) ? name[..^6] : name;
}
