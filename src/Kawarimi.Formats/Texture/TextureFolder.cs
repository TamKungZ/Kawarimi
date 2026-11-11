namespace Kawarimi.Formats;

public static class TextureFolder
{
    public sealed record Result(List<Ntp3Texture> Written, List<Ntp3Texture> Skipped);

    public static Result Export(XfbinFile x, string dir)
    {
        Directory.CreateDirectory(dir);
        var r = new Result(new(), new());
        foreach (var t in Ntp3Texture.FindAll(x))
        {
            if (!t.IsSupported)
            {
                r.Skipped.Add(t);
                continue;
            }
            File.WriteAllBytes(Path.Combine(dir, t.FileName), PngImage.Encode(t.Width, t.Height, t.DecodeRgb(x)));
            r.Written.Add(t);
        }
        return r;
    }

    public static List<Ntp3Texture> Import(XfbinFile x, string dir)
    {
        var changed = new List<Ntp3Texture>();
        foreach (var t in Ntp3Texture.FindAll(x).Where(t => t.IsSupported))
        {
            var png = Find(dir, t);
            if (png == null) continue;
            var (w, h, rgb) = PngImage.Decode(File.ReadAllBytes(png));
            if (t.Replace(x, w, h, rgb)) changed.Add(t);
        }
        return changed;
    }

    static string? Find(string dir, Ntp3Texture t)
    {
        var exact = Path.Combine(dir, t.FileName);
        if (File.Exists(exact)) return exact;
        if (!Directory.Exists(dir)) return null;
        return Directory.EnumerateFiles(dir, $"tex_{t.Chunk:D3}_{t.Index}_*.png").OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault();
    }
}
