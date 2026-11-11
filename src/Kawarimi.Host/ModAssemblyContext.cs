using System.Reflection;
using System.Runtime.Loader;

namespace Kawarimi.Host;

internal sealed class ModAssemblyContext : AssemblyLoadContext
{
    readonly AssemblyDependencyResolver _resolver;

    public ModAssemblyContext(string path) : base(Path.GetFileNameWithoutExtension(path), isCollectible: false)
        => _resolver = new AssemblyDependencyResolver(path);

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name == "Kawarimi.Api") return typeof(IKawarimiMod).Assembly;
        var p = _resolver.ResolveAssemblyToPath(name);
        return p == null ? null : LoadFromAssemblyPath(p);
    }
}
