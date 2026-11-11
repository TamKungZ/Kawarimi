using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Kawarimi.Formats;

public static class PngImage
{
    static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10];

    public static byte[] Encode(int w, int h, byte[] rgb)
    {
        var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
            for (int y = 0; y < h; y++)
            {
                z.WriteByte(0);
                z.Write(rgb, y * w * 3, w * 3);
            }
        var ms = new MemoryStream();
        ms.Write(Signature);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, w);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
        ihdr[8] = 8;
        ihdr[9] = 2;
        WriteChunk(ms, "IHDR", ihdr);
        WriteChunk(ms, "IDAT", raw.ToArray());
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    public static (int w, int h, byte[] rgb) Decode(byte[] f)
    {
        if (f.Length < 8 || !f.AsSpan(0, 8).SequenceEqual(Signature)) throw new InvalidDataException("not a PNG");
        int p = 8, w = 0, h = 0, depth = 0, type = 0, interlace = 0;
        byte[] palette = [];
        var idat = new MemoryStream();
        while (p + 8 <= f.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(f.AsSpan(p));
            string t = Encoding.ASCII.GetString(f, p + 4, 4);
            var d = f.AsSpan(p + 8, len);
            if (t == "IHDR")
            {
                w = BinaryPrimitives.ReadInt32BigEndian(d);
                h = BinaryPrimitives.ReadInt32BigEndian(d[4..]);
                depth = d[8];
                type = d[9];
                interlace = d[12];
            }
            else if (t == "PLTE") palette = d.ToArray();
            else if (t == "IDAT") idat.Write(d);
            else if (t == "IEND") break;
            p += 12 + len;
        }
        if (depth != 8 || interlace != 0) throw new InvalidDataException("save the PNG as 8-bit, non-interlaced");
        int bpp = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException("PNG color type " + type) };
        idat.Position = 0;
        var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        var px = Unfilter(raw.ToArray(), w, h, bpp);
        var rgb = new byte[w * h * 3];
        for (int i = 0; i < w * h; i++)
            for (int c = 0; c < 3; c++)
                rgb[3 * i + c] = type switch
                {
                    0 or 4 => px[i * bpp],
                    3 => palette[px[i] * 3 + c],
                    _ => px[i * bpp + c],
                };
        return (w, h, rgb);
    }

    static byte[] Unfilter(byte[] src, int w, int h, int bpp)
    {
        int stride = w * bpp;
        var px = new byte[h * stride];
        for (int y = 0; y < h; y++)
        {
            int filter = src[y * (stride + 1)], s = y * (stride + 1) + 1, o = y * stride;
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? px[o + i - bpp] : 0, b = y > 0 ? px[o - stride + i] : 0, c = i >= bpp && y > 0 ? px[o - stride + i - bpp] : 0;
                int pred = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException("bad PNG filter"),
                };
                px[o + i] = (byte)(src[s + i] + pred);
            }
        }
        return px;
    }

    static int Paeth(int a, int b, int c)
    {
        int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    static void WriteChunk(Stream s, string type, byte[] data)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, data.Length);
        s.Write(b);
        var td = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        s.Write(td);
        BinaryPrimitives.WriteUInt32BigEndian(b, Crc32(td));
        s.Write(b);
    }

    static uint Crc32(byte[] d)
    {
        uint c = 0xffffffff;
        foreach (var x in d)
        {
            c ^= x;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
        }
        return ~c;
    }
}
