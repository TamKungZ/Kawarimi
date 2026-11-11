using System.Buffers.Binary;
using System.Text;

namespace Kawarimi.Formats;

public sealed class XfbinFile
{
    public uint Version { get; private set; }
    public byte[] Header { get; private set; } = [];
    public List<XfbinChunk> Chunks { get; } = new();
    public IReadOnlyList<string> TypeNames { get; private set; } = [];
    public IReadOnlyList<string> PathNames { get; private set; } = [];
    public IReadOnlyList<string> ChunkNames { get; private set; } = [];
    public IReadOnlyList<(int Type, int Path, int Name)> Map { get; private set; } = [];
    public IReadOnlyList<int> MapIndices { get; private set; } = [];

    int[] _chunkMap = [];
    int[] _chunkPage = [];

    public static bool HasMagic(ReadOnlySpan<byte> d) => d.Length >= 8 && d[0] == 'N' && d[1] == 'U' && d[2] == 'C' && d[3] == 'C';

    public static XfbinFile Parse(ReadOnlySpan<byte> data)
    {
        if (!TryParse(data, strictEnd: true, out var f, out var length, out var error) || length != data.Length)
            throw new InvalidDataException(error ?? "Not a valid xfbin");
        return f!;
    }

    public static bool TryMeasure(ReadOnlySpan<byte> buffer, out int length, out XfbinFile? file) =>
        TryParse(buffer, strictEnd: false, out file, out length, out _);

    public string Describe(XfbinChunk c) => Describe(Chunks.IndexOf(c));

    public string Describe(int chunk) => TypeOf(chunk) + " : " + NameOf(chunk);

    public string TypeOf(int chunk) => MapEntry(chunk) is { } m && m.Type < TypeNames.Count ? TypeNames[m.Type] : "?";

    public string NameOf(int chunk) => MapEntry(chunk) is { } m && m.Name < ChunkNames.Count ? ChunkNames[m.Name] : "?";

    public int? Reference(int fromChunk, uint reference)
    {
        if (fromChunk < 0 || fromChunk >= _chunkPage.Length) return null;
        long slot = _chunkPage[fromChunk] + (long)reference;
        if (slot >= MapIndices.Count) return null;
        int target = MapIndices[(int)slot];
        int found = Array.IndexOf(_chunkMap, target);
        return found >= 0 ? found : null;
    }

    (int Type, int Path, int Name)? MapEntry(int chunk)
    {
        if (chunk < 0 || chunk >= _chunkMap.Length) return null;
        int m = _chunkMap[chunk];
        return m >= 0 && m < Map.Count ? Map[m] : null;
    }

    void ResolvePages()
    {
        _chunkMap = new int[Chunks.Count];
        _chunkPage = new int[Chunks.Count];
        int page = 0;
        for (int i = 0; i < Chunks.Count; i++)
        {
            var c = Chunks[i];
            long slot = page + (long)c.MapIndex;
            _chunkPage[i] = page;
            _chunkMap[i] = MapIndices.Count == 0 ? (int)c.MapIndex : slot < MapIndices.Count ? MapIndices[(int)slot] : -1;
            if (TypeOf(i) == "nuccChunkPage" && c.Data.Length >= 4) page += (int)U32(c.Data, 0);
        }
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream(Header.Length + Chunks.Sum(c => c.Data.Length + 12));
        ms.Write(Header);
        Span<byte> h = stackalloc byte[12];
        foreach (var c in Chunks)
        {
            WriteChunkHeader(h, c.Data.Length, c.MapIndex, c.Version, c.Flags);
            ms.Write(h);
            ms.Write(c.Data);
        }
        return ms.ToArray();
    }

    public static void WriteChunkHeader(Span<byte> h, int size, uint mapIndex, ushort version, ushort flags)
    {
        BinaryPrimitives.WriteUInt32BigEndian(h, (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(h[4..], mapIndex);
        BinaryPrimitives.WriteUInt16BigEndian(h[8..], version);
        BinaryPrimitives.WriteUInt16BigEndian(h[10..], flags);
    }

    public int ReplaceToken(string oldToken, string newToken)
    {
        if (oldToken.Length != newToken.Length || oldToken.Length == 0) throw new ArgumentException("Tokens must have the same non-zero length.");
        byte[] o = Encoding.ASCII.GetBytes(oldToken), n = Encoding.ASCII.GetBytes(newToken);
        int count = Replace(Header, o, n);
        foreach (var c in Chunks) count += Replace(c.Data, o, n);
        ParseTables(Header);
        ResolvePages();
        return count;
    }

    static uint U32(ReadOnlySpan<byte> d, int o) => BinaryPrimitives.ReadUInt32BigEndian(d[o..]);

    static IEnumerable<int> ChunkStartCandidates(ReadOnlySpan<byte> d, uint version)
    {
        uint ms = U32(d, 16);
        long extra = d.Length >= 72 ? U32(d, 64) : 0;
        long[] c = { 28 + ms + 8 * extra, 40 + ms + 8 * extra, version >= 0x70 ? 40 + ms : 28 + ms, version >= 0x70 ? 28 + ms : 40 + ms };
        return c.Where(x => x < int.MaxValue).Select(x => (int)x).Distinct().ToList();
    }

    static bool TryParse(ReadOnlySpan<byte> d, bool strictEnd, out XfbinFile? file, out int length, out string? error)
    {
        file = null;
        length = 0;
        error = null;
        if (!HasMagic(d) || d.Length < 40)
        {
            error = "Missing NUCC magic";
            return false;
        }
        uint version = U32(d, 4), mapSize = U32(d, 16);
        if (version < 0x60 || version > 0x90 || mapSize > 0x100000 || mapSize + 40 > d.Length)
        {
            error = "Implausible header";
            return false;
        }
        int mapCount = (int)Math.Max(U32(d, 52), U32(d, 60));
        foreach (int start in ChunkStartCandidates(d, version))
        {
            if (start + 12 > d.Length) continue;
            var f = new XfbinFile { Version = version, Header = d[..start].ToArray() };
            int end = f.ReadChunks(d, start, mapCount);
            if (end < 0 || (strictEnd && end != d.Length)) continue;
            f.ParseTables(f.Header);
            f.ResolvePages();
            file = f;
            length = end;
            return true;
        }
        error = "Chunk walk did not line up";
        return false;
    }

    int ReadChunks(ReadOnlySpan<byte> d, int start, int mapCount)
    {
        int p = start;
        while (p + 12 <= d.Length)
        {
            uint size = U32(d, p), idx = U32(d, p + 4);
            ushort ver = BinaryPrimitives.ReadUInt16BigEndian(d[(p + 8)..]);
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(d[(p + 10)..]);
            if (ver < 0x60 || ver > 0x90 || idx >= Math.Max(mapCount, 1) + 64 || size > (uint)(d.Length - p - 12)) break;
            Chunks.Add(new XfbinChunk { MapIndex = idx, Version = ver, Flags = flags, Data = d.Slice(p + 12, (int)size).ToArray() });
            p += 12 + (int)size;
        }
        return Chunks.Count == 0 ? -1 : p;
    }

    void ParseTables(byte[] d)
    {
        try
        {
            int typeCount = (int)U32(d, 28), typeSize = (int)U32(d, 32), pathCount = (int)U32(d, 36), pathSize = (int)U32(d, 40);
            int nameCount = (int)U32(d, 44), nameSize = (int)U32(d, 48), mapCount = (int)U32(d, 52);
            int p = Version >= 0x70 ? 68 : 64;
            List<string> Read(int count, int size)
            {
                var list = new List<string>();
                int q = p, end = p + size;
                for (int i = 0; i < count && q < end; i++)
                {
                    int e = q;
                    while (e < end && d[e] != 0) e++;
                    list.Add(Encoding.UTF8.GetString(d, q, e - q));
                    q = e + 1;
                }
                p = end;
                return list;
            }
            TypeNames = Read(typeCount, typeSize);
            PathNames = Read(pathCount, pathSize);
            ChunkNames = Read(nameCount, nameSize);
            p = (p + 3) & ~3;
            var map = new List<(int, int, int)>();
            for (int i = 0; i < mapCount && p + 12 <= Header.Length; i++, p += 12)
                map.Add(((int)U32(d, p), (int)U32(d, p + 4), (int)U32(d, p + 8)));
            Map = map;
            int indexCount = (int)U32(d, 60), extraCount = (int)U32(d, 64);
            p += 8 * extraCount;
            var indices = new List<int>();
            for (int i = 0; i < indexCount && p + 4 <= Header.Length; i++, p += 4) indices.Add((int)U32(d, p));
            MapIndices = indices.Count == indexCount ? indices : [];
        }
        catch (Exception)
        {
        }
    }

    static int Replace(byte[] buf, byte[] o, byte[] n)
    {
        int count = 0, i = 0;
        while (true)
        {
            int k = buf.AsSpan(i).IndexOf(o);
            if (k < 0) break;
            n.CopyTo(buf, i + k);
            i += k + o.Length;
            count++;
        }
        return count;
    }
}
