using System.Buffers.Binary;
using System.Text;

namespace Kawarimi.Formats;

public sealed class UtfTable
{
    public List<Dictionary<string, object?>> Rows { get; } = new();

    public static T? Get<T>(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v != null ? (T)Convert.ChangeType(v, typeof(T)) : default;

    public static UtfTable Read(Stream fs, long pos)
    {
        var buf = ReadDecrypted(fs, pos);
        int size = buf.Length;
        int rowsOff = U16(buf, 0xA) + 8, strOff = (int)U32(buf, 0xC) + 8;
        int nCols = U16(buf, 0x18), rowW = U16(buf, 0x1A);
        long nRows = U32(buf, 0x1C);
        if (nRows * Math.Max(rowW, 1) > size || nCols > 512) throw new InvalidDataException("Implausible @UTF table");

        var cols = new List<(string name, int flag, object? constant)>();
        int p = 0x20;
        for (int i = 0; i < nCols; i++)
        {
            int flag = buf[p++];
            string name = Str(buf, strOff + (int)U32(buf, p));
            p += 4;
            object? cv = (flag & 0xF0) == 0x30 ? Value(buf, ref p, flag & 0xF, strOff) : null;
            cols.Add((name, flag, cv));
        }

        var t = new UtfTable();
        for (int r = 0; r < nRows; r++)
        {
            var row = new Dictionary<string, object?>();
            int q = rowsOff + r * rowW;
            foreach (var (name, flag, cv) in cols)
                row[name] = (flag & 0xF0) switch
                {
                    0x50 => Value(buf, ref q, flag & 0xF, strOff),
                    0x30 => cv,
                    _ => null,
                };
            t.Rows.Add(row);
        }
        return t;
    }

    static byte[] ReadDecrypted(Stream fs, long pos)
    {
        var head = new byte[4];
        fs.Position = pos;
        fs.ReadExactly(head);
        bool enc = Encoding.ASCII.GetString(head) != "@UTF";
        var buf = new byte[Math.Min(0x10000, fs.Length - pos)];
        fs.Position = pos;
        fs.ReadExactly(buf);
        if (enc) Deobfuscate(buf);
        if (Encoding.ASCII.GetString(buf, 0, 4) != "@UTF") throw new InvalidDataException("Bad @UTF at " + pos);
        long size = (long)U32(buf, 4) + 8;
        if (size > 64 * 1024 * 1024 || pos + size > fs.Length) throw new InvalidDataException("Implausible @UTF size " + size);
        if (size > buf.Length)
        {
            buf = new byte[size];
            fs.Position = pos;
            fs.ReadExactly(buf);
            if (enc) Deobfuscate(buf);
        }
        return buf;
    }

    static object? Value(byte[] b, ref int p, int type, int strOff)
    {
        switch (type)
        {
            case 0: return (long)b[p++];
            case 1: return (long)(sbyte)b[p++];
            case 2: { var v = U16(b, p); p += 2; return (long)v; }
            case 3: { var v = (short)U16(b, p); p += 2; return (long)v; }
            case 4: { var v = U32(b, p); p += 4; return (long)v; }
            case 5: { var v = (int)U32(b, p); p += 4; return (long)v; }
            case 6: case 7: { var v = (long)BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(p)); p += 8; return v; }
            case 8: { var v = BinaryPrimitives.ReadSingleBigEndian(b.AsSpan(p)); p += 4; return (double)v; }
            case 0xA: { var s = Str(b, strOff + (int)U32(b, p)); p += 4; return s; }
            case 0xB: p += 8; return null;
            default: throw new InvalidDataException("UTF type " + type);
        }
    }

    static ushort U16(byte[] b, int p) => BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(p));
    static uint U32(byte[] b, int p) => BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(p));

    static string Str(byte[] b, int p)
    {
        int e = p;
        while (b[e] != 0) e++;
        return Encoding.UTF8.GetString(b, p, e - p);
    }

    static void Deobfuscate(byte[] d)
    {
        uint m = 0x655F;
        const uint t = 0x4115;
        for (int i = 0; i < d.Length; i++)
        {
            d[i] ^= (byte)m;
            m *= t;
        }
    }
}
