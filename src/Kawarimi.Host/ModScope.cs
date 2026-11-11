namespace Kawarimi.Host;

internal sealed class ModScope : ISkinRegistry
{
    readonly SkinRegistry _registry;
    readonly List<SkinHandle> _mine = new();

    public ModScope(SkinRegistry registry, string modId)
    {
        _registry = registry;
        ModId = modId;
    }

    public string ModId { get; set; }
    public List<string> Warnings { get; } = new();
    public int Count => _mine.Count;
    public IReadOnlyList<SkinDefinition> All => _registry.All;

    public SkinHandle Register(SkinDefinition skin)
    {
        skin = skin with { ModId = ModId };
        skin.Validate();
        if (!Directory.Exists(skin.ModelDirectory))
            throw new InvalidOperationException($"skin [{skin.SkinCode}]: folder not found: {skin.ModelDirectory}");
        if (MissingFilesWarning(skin) is { } w) Warnings.Add(w);
        var h = _registry.Register(skin);
        _mine.Add(h);
        return h;
    }

    public bool Unregister(SkinHandle handle)
    {
        _mine.Remove(handle);
        return _registry.Unregister(handle);
    }

    public void Rollback()
    {
        foreach (var h in _mine.ToArray()) _registry.Unregister(h);
        _mine.Clear();
    }

    static string? MissingFilesWarning(SkinDefinition skin)
    {
        if (!string.IsNullOrEmpty(skin.Replaces))
            return Directory.EnumerateFiles(skin.ModelDirectory, skin.Replaces + "*").Any()
                ? null
                : $"skin [{skin.SkinCode}]: no {skin.Replaces}* file in {skin.ModelDirectory} (nothing would be replaced)";
        return File.Exists(Path.Combine(skin.ModelDirectory, skin.SkinCode + "bod1.xfbin"))
            ? null
            : $"skin [{skin.SkinCode}]: {skin.SkinCode}bod1.xfbin not found in {skin.ModelDirectory} (the model will not load)";
    }
}
