namespace Kawarimi.Formats;

public static class ModelFolder
{
    public static (string objPath, List<NudPolygon> polys, List<string> warnings) Export(XfbinFile x, string sourceName, string dir, string? textureFolder = null)
    {
        Directory.CreateDirectory(dir);
        var warnings = new List<string>();
        var polys = NudReader.Read(x, warnings);
        string stem = Path.GetFileNameWithoutExtension(sourceName);
        var (obj, mtl) = ObjExporter.Write(x, polys, sourceName, stem + ".mtl", textureFolder);
        string objPath = Path.Combine(dir, stem + ".obj");
        File.WriteAllText(objPath, obj);
        File.WriteAllText(Path.Combine(dir, stem + ".mtl"), mtl);
        return (objPath, polys, warnings);
    }

    public static MeshImportResult Import(XfbinFile x, string objPath, bool normals)
    {
        var names = NudReader.Read(x).Select(p => p.ObjName).ToHashSet();
        return MeshImporter.Apply(x, ObjDocument.Parse(File.ReadLines(objPath), names), normals);
    }
}
