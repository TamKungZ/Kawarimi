namespace Kawarimi.Host;

internal static class LegacyMod
{
    const int CodeLength = 4;

    public static bool IsContainer(string dir) =>
        !File.Exists(Path.Combine(dir, ModManifest.FileName)) &&
        Directory.GetFiles(dir, "*.dll").Length == 0 &&
        FilesFolder(dir) is null &&
        Directory.GetDirectories(dir).Any(d => FilesFolder(d) is not null);

    public static string? FilesFolder(string dir)
    {
        foreach (var candidate in new[] { dir, Path.Combine(dir, "files"), Path.Combine(dir, "data", "spc"), Path.Combine(dir, "files", "data", "spc") })
            if (Directory.Exists(candidate) && XfbinFiles(candidate).Any()) return candidate;
        return null;
    }

    public static IEnumerable<SkinDefinition> Skins(string modDir, string files)
    {
        var name = Path.GetFileName(modDir.TrimEnd('\\', '/'));
        foreach (var code in XfbinFiles(files).Select(f => Path.GetFileName(f)[..CodeLength]).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return new SkinDefinition
            {
                CharacterCode = "legacy",
                SkinCode = SkinCodeFor(modDir, code),
                Replaces = code,
                DisplayName = name,
                ModelDirectory = Path.GetFullPath(files),
            };
    }

    static IEnumerable<string> XfbinFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.xfbin").Where(f => IsCode(Path.GetFileName(f)));

    static bool IsCode(string fileName) =>
        fileName.Length > CodeLength && fileName[..CodeLength].All(char.IsAsciiLetterOrDigit);

    static string SkinCodeFor(string modDir, string code)
    {
        uint h = 2166136261;
        foreach (var c in Path.GetFullPath(modDir).ToLowerInvariant() + "|" + code.ToLowerInvariant()) h = (h ^ c) * 16777619;
        return "L" + h.ToString("x8")[..6];
    }
}
