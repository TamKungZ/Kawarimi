namespace Kawarimi;

public sealed class SkinRegistry : ISkinRegistry
{
    readonly object _gate = new();
    readonly List<(int id, SkinDefinition skin)> _skins = new();
    int _next = 1;

    public event Action? Changed;

    public SkinHandle Register(SkinDefinition skin)
    {
        ArgumentNullException.ThrowIfNull(skin);
        skin.Validate();
        SkinHandle h;
        lock (_gate)
        {
            var clash = _skins.FirstOrDefault(s => string.Equals(s.skin.SkinCode, skin.SkinCode, StringComparison.OrdinalIgnoreCase));
            if (clash.skin != null)
            {
                string owner = string.IsNullOrEmpty(clash.skin.ModId) ? "another mod" : $"mod [{clash.skin.ModId}]";
                throw new InvalidOperationException($"SkinCode [{skin.SkinCode}] is already registered by {owner}.");
            }
            h = new SkinHandle(_next++);
            _skins.Add((h.Id, skin));
        }
        Changed?.Invoke();
        return h;
    }

    public bool Unregister(SkinHandle handle)
    {
        bool removed;
        lock (_gate) removed = _skins.RemoveAll(s => s.id == handle.Id) > 0;
        if (removed) Changed?.Invoke();
        return removed;
    }

    public void Clear()
    {
        lock (_gate) _skins.Clear();
        Changed?.Invoke();
    }

    public IReadOnlyList<SkinDefinition> All
    {
        get { lock (_gate) return _skins.Select(s => s.skin).ToArray(); }
    }
}
