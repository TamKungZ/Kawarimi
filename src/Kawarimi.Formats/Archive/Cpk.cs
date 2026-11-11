using System.Text;

namespace Kawarimi.Formats;

public sealed record CpkEntry(string Dir, string Name, long Offset, long Size, long ExtractSize)
{
    public string Path => string.IsNullOrEmpty(Dir) ? Name : Dir + "/" + Name;
}

public sealed class Cpk
{
    public string FilePath { get; }
    public List<CpkEntry> Entries { get; } = new();

    public Cpk(string path)
    {
        FilePath = path;
        using var fs = Open(path);
        var magic = new byte[4];
        fs.ReadExactly(magic);
        if (Encoding.ASCII.GetString(magic) != "CPK ") throw new InvalidDataException("Not a CPK");
        var hdr = UtfTable.Read(fs, 0x10).Rows[0];
        long toc = UtfTable.Get<long>(hdr, "TocOffset"), content = UtfTable.Get<long>(hdr, "ContentOffset");
        long baseOffset = content > 0 && content < toc ? content : toc;
        foreach (var r in UtfTable.Read(fs, toc + 0x10).Rows)
            Entries.Add(new CpkEntry(UtfTable.Get<string>(r, "DirName") ?? "", UtfTable.Get<string>(r, "FileName") ?? "",
                baseOffset + UtfTable.Get<long>(r, "FileOffset"), UtfTable.Get<long>(r, "FileSize"), UtfTable.Get<long>(r, "ExtractSize")));
    }

    public byte[] ReadRaw(CpkEntry e)
    {
        var raw = new byte[e.Size];
        using var fs = Open(FilePath);
        fs.Position = e.Offset;
        fs.ReadExactly(raw);
        return raw;
    }

    public byte[] ReadPlain(CpkEntry e)
    {
        var plain = StormCipher.Decode(ReadRaw(e));
        return plain.Length > e.ExtractSize ? plain[..(int)e.ExtractSize] : plain;
    }

    static FileStream Open(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
}
