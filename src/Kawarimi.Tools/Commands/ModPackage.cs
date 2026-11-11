using System.IO.Compression;

namespace Kawarimi.Tools.Commands;

internal static class ModPackage
{
    public static string Zip(string modDir, string shareDir)
    {
        Directory.CreateDirectory(shareDir);
        string zip = Path.Combine(shareDir, Path.GetFileName(modDir) + ".zip");
        if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(modDir, zip, CompressionLevel.Optimal, includeBaseDirectory: true);
        return zip;
    }
}
