namespace Kawarimi;

public interface ISkinRegistry
{
    SkinHandle Register(SkinDefinition skin);
    bool Unregister(SkinHandle handle);
    IReadOnlyList<SkinDefinition> All { get; }
}
