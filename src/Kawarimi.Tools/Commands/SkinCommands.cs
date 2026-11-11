using Kawarimi.Formats;
using Kawarimi.Host;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class SkinCommands
{
    public static int Compose(string extracted, string gameplay, string model, string code, string character, string name, string modDir)
    {
        var r = SkinBuilder.Compose(new SkinBuilder.ComposeOptions(extracted, gameplay, model == "-" ? null : model, code, character, name, modDir));
        Ui.Ok($"Skin [{code}] for [{character}] written to {modDir}: {r.FilesWritten} file(s)");
        Ui.Info($"  model from [{(model == "-" ? gameplay : model)}]: {r.FromModel.Count} file(s)   gameplay from [{gameplay}]: {r.FromGameplay.Count} file(s)");
        r.Warnings.ForEach(Ui.Warn);
        return r.Warnings.Count == 0 ? 0 : 3;
    }

    public static int Compare(string extracted, string codeA, string codeB)
    {
        var spc = SkinBuilder.FindSpc(extracted) ?? throw new DirectoryNotFoundException("No data/spc files found.");
        string a = Path.Combine(spc, codeA + "bod1.xfbin"), b = Path.Combine(spc, codeB + "bod1.xfbin");
        foreach (var f in new[] { a, b })
            if (!File.Exists(f))
            {
                Ui.Error("Not extracted: " + Path.GetFileName(f));
                return 1;
            }
        var r = ModelComparer.Compare(a, codeA, b, codeB);
        Ui.Info($"{codeA}bod1 vs {codeB}bod1 (names normalised, {r.Shared} shared)");
        foreach (var (type, (ca, cb)) in r.TypeCounts) Ui.Info($"  {type,-28} {codeA}: {ca,4}   {codeB}: {cb,4}");
        Ui.Info($"  only in {codeA}: {r.OnlyInA.Count}   only in {codeB}: {r.OnlyInB.Count}");
        foreach (var n in r.OnlyInA.Take(40)) Ui.Info($"    - {n}");
        foreach (var n in r.OnlyInB.Take(40)) Ui.Info($"    + {n}");
        if (r.SameNames) Ui.Ok("IDENTICAL name tables: bones/meshes/materials line up");
        else Ui.Warn("DIFFERENT name tables: animations from one may not bind fully to the other");
        return 0;
    }

    public static int Verify(string modDir)
    {
        var json = Path.Combine(modDir, ModManifest.FileName);
        if (!File.Exists(json))
        {
            Ui.Error("No skin.json in " + modDir);
            return 1;
        }
        var man = ModManifest.Parse(modDir, File.ReadAllText(json));
        int problems = 0;
        void Bad(string m)
        {
            Ui.Error("  PROBLEM: " + m);
            problems++;
        }
        if (man.ApiVersion > KawarimiApi.Version) Bad($"needs API {man.ApiVersion}, this build is {KawarimiApi.Version}");
        foreach (var s in man.Skins)
        {
            Ui.Info($"skin [{s.SkinCode}] for [{s.CharacterCode ?? s.CharacterId.ToString()}] folder {s.ModelDirectory}");
            try
            {
                s.Validate();
            }
            catch (Exception e)
            {
                Bad(e.Message);
                continue;
            }
            if (!Directory.Exists(s.ModelDirectory))
            {
                Bad("folder missing");
                continue;
            }
            var files = Directory.GetFiles(s.ModelDirectory, s.FileCode + "*");
            if (s.Replaces == null && !files.Any(f => Path.GetFileName(f).Equals(s.SkinCode + "bod1.xfbin", StringComparison.OrdinalIgnoreCase)))
                Bad($"{s.SkinCode}bod1.xfbin missing");
            foreach (var f in files.Where(f => f.EndsWith(".xfbin", StringComparison.OrdinalIgnoreCase)))
            {
                try { XfbinFile.Parse(File.ReadAllBytes(f)); }
                catch (Exception e) { Bad($"{Path.GetFileName(f)}: {e.Message} (not a plain xfbin)"); }
            }
            Ui.Info($"  {files.Length} file(s) checked");
        }
        if (problems == 0) Ui.Ok("OK");
        else Ui.Error($"{problems} problem(s)");
        return problems == 0 ? 0 : 1;
    }
}
