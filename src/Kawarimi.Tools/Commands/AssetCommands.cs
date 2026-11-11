using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class AssetCommands
{
    public static int ExportTextures(string xfbin, string dir)
    {
        var r = TextureFolder.Export(Load(xfbin), dir);
        foreach (var t in r.Written) Ui.Info($"  {t.FileName}  ({t.MipSizes.Length} mip level(s))");
        foreach (var t in r.Skipped) Ui.Warn($"chunk {t.Chunk} texture {t.Index}: format {t.Format} not supported yet");
        Ui.Ok($"{r.Written.Count} texture(s) exported to {dir}");
        return 0;
    }

    public static int ImportTextures(string xfbin, string dir, string output)
    {
        var x = Load(xfbin);
        var changed = TextureFolder.Import(x, dir);
        foreach (var t in changed) Ui.Info($"  replaced {t.FileName}");
        if (changed.Count == 0)
        {
            Ui.Warn("no changed PNG found (file names must keep the exported tex_<chunk>_<index>_ prefix)");
            return 1;
        }
        File.WriteAllBytes(output, x.ToBytes());
        Ui.Ok($"{changed.Count} texture(s) imported -> {output}");
        return 0;
    }

    public static int ExportModel(string xfbin, string dir)
    {
        var x = Load(xfbin);
        TextureFolder.Export(x, Path.Combine(dir, "textures"));
        var (objPath, polys, warnings) = ModelFolder.Export(x, Path.GetFileName(xfbin), dir, "textures");
        warnings.ForEach(Ui.Warn);
        Ui.Ok($"{polys.Count} polygon group(s), {polys.Sum(p => p.VertCount)} vertices -> {objPath}");
        return 0;
    }

    public static int ImportModel(string xfbin, string obj, string output, bool normals)
    {
        var x = Load(xfbin);
        var r = ModelFolder.Import(x, obj, normals);
        foreach (var (name, moved) in r.Changed.Where(c => !r.Rebuilt.Contains(c.Name))) Ui.Info($"  {name}: {moved} vertex position(s) changed");
        if (ModProject.Describe(r) is { } note) Ui.Info($"  {note}");
        File.WriteAllBytes(output, x.ToBytes());
        Ui.Ok($"{r.Changed.Count} polygon group(s) changed -> {output}");
        return 0;
    }

    static XfbinFile Load(string path) => XfbinFile.Parse(File.ReadAllBytes(path));
}
