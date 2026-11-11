using System.Text.Json;

namespace Kawarimi.Formats;

public static class SkinBuilder
{
    public sealed record ComposeOptions(
        string CapturedDir,
        string GameplayCode,
        string? ModelCode,
        string NewCode,
        string Character,
        string DisplayName,
        string ModDir,
        string ModelPrefixes = "bod,acc");

    public sealed record ComposeResult(int FilesWritten, List<string> FromModel, List<string> FromGameplay, List<string> Warnings);

    public static string? FindSpc(string captured)
    {
        foreach (var p in new[] { Path.Combine(captured, "files", "data", "spc"), Path.Combine(captured, "data", "spc"), captured })
            if (Directory.Exists(p) && Directory.GetFiles(p, "*.xfbin").Length > 0) return p;
        return null;
    }

    public static int RetargetFile(string input, string oldCode, string newCode, string output)
    {
        var data = File.ReadAllBytes(input);
        int n = 0;
        if (XfbinFile.HasMagic(data))
        {
            var x = XfbinFile.Parse(data);
            n = x.ReplaceToken(oldCode, newCode);
            data = x.ToBytes();
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllBytes(output, data);
        return n;
    }

    public static ComposeResult Compose(ComposeOptions o)
    {
        string model = o.ModelCode ?? o.GameplayCode;
        if (o.GameplayCode.Length != o.NewCode.Length || model.Length != o.NewCode.Length)
            throw new ArgumentException($"Skin code and source codes must have the same length ({o.GameplayCode.Length}).");
        var spc = FindSpc(o.CapturedDir) ?? throw new DirectoryNotFoundException($"No data/spc files under {o.CapturedDir}. Run 'extract' first.");
        var prefixes = o.ModelPrefixes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        bool IsModelPart(string suffix) => prefixes.Any(p => suffix.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        var res = new ComposeResult(0, new(), new(), new());
        var filesDir = Path.Combine(o.ModDir, "files");
        Directory.CreateDirectory(filesDir);
        int written = 0;

        void Take(string source, Func<string, bool> accept, List<string> into)
        {
            foreach (var f in Directory.GetFiles(spc, source + "*"))
            {
                var suffix = Path.GetFileName(f)[source.Length..];
                if (suffix.Length == 0 || char.IsDigit(suffix[0]) || !accept(suffix)) continue;
                RetargetFile(f, source, o.NewCode, Path.Combine(filesDir, o.NewCode + suffix));
                into.Add(Path.GetFileName(f));
                written++;
            }
        }

        if (string.Equals(model, o.GameplayCode, StringComparison.OrdinalIgnoreCase))
        {
            Take(o.GameplayCode, _ => true, res.FromGameplay);
        }
        else
        {
            Take(model, IsModelPart, res.FromModel);
            Take(o.GameplayCode, s => !IsModelPart(s), res.FromGameplay);
            if (res.FromModel.Count == 0) res.Warnings.Add($"no model files for [{model}] (bod*/acc*)");
        }
        if (!File.Exists(Path.Combine(filesDir, o.NewCode + "bod1.xfbin"))) res.Warnings.Add($"{model}bod1.xfbin is missing - the model will not load");
        if (!res.FromGameplay.Any(n => n.Contains("prm", StringComparison.OrdinalIgnoreCase)))
            res.Warnings.Add($"no prm file for [{o.GameplayCode}] - the character will have no moveset parameters");

        var manifest = new
        {
            id = o.NewCode,
            name = o.DisplayName,
            version = "1.0",
            apiVersion = KawarimiApi.Version,
            skins = new[] { new { character = o.Character, skinCode = o.NewCode, name = o.DisplayName, files = "files" } },
            source = new { model, gameplay = o.GameplayCode },
        };
        File.WriteAllText(Path.Combine(o.ModDir, ModProject.ManifestFile), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        return res with { FilesWritten = written };
    }
}
