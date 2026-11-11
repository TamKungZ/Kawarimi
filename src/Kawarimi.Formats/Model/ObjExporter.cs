using System.Globalization;
using System.Text;

namespace Kawarimi.Formats;

public static class ObjExporter
{
    public static (string obj, string mtl) Write(XfbinFile x, IReadOnlyList<NudPolygon> polys, string sourceName, string mtlName, string? textureFolder = null)
    {
        var textures = Ntp3Texture.FindAll(x);
        var materials = new Dictionary<string, string?>();
        var obj = new StringBuilder();
        obj.AppendLine($"# Kawarimi export of {sourceName}: meters, Y up, one object per polygon group");
        obj.AppendLine($"mtllib {mtlName}");
        int vBase = 1;
        foreach (var p in polys)
        {
            var d = x.Chunks[p.Chunk].Data;
            var texture = textureFolder == null ? null : ModelTextures.For(x, p, textures);
            string material = texture == null ? NudPolygon.SafeName(p.Name) : Path.GetFileNameWithoutExtension(texture.FileName);
            materials.TryAdd(material, texture == null ? null : textureFolder + "/" + texture.FileName);
            obj.AppendLine($"o {p.ObjName}");
            for (int i = 0; i < p.VertCount; i++) obj.AppendLine("v " + Join(ObjSpace.PositionToObj(p.Position(d, i))));
            for (int i = 0; i < p.VertCount; i++) obj.AppendLine("vt " + Join(p.ObjUv(d, i)));
            if (p.HasNormals)
                for (int i = 0; i < p.VertCount; i++) obj.AppendLine("vn " + Join(ObjSpace.NormalToObj(p.Normal(d, i))));
            obj.AppendLine($"usemtl {material}");
            foreach (var (a, b, c) in p.Triangles())
                obj.AppendLine($"f {Corner(a + vBase, p.HasNormals)} {Corner(b + vBase, p.HasNormals)} {Corner(c + vBase, p.HasNormals)}");
            vBase += p.VertCount;
        }
        var mtl = new StringBuilder();
        foreach (var (name, map) in materials)
        {
            mtl.AppendLine($"newmtl {name}").AppendLine("Kd 1 1 1");
            if (map != null) mtl.AppendLine($"map_Kd {map}");
            mtl.AppendLine();
        }
        return (obj.ToString(), mtl.ToString());
    }

    static string Corner(int i, bool normals) => normals ? $"{i}/{i}/{i}" : $"{i}/{i}";

    static string Join(float[] v) => string.Join(' ', v.Select(f => f.ToString("G9", CultureInfo.InvariantCulture)));
}
