using System.Buffers.Binary;
using System.Text;

namespace Kawarimi.Formats;

public sealed record Ntp3Texture(int Chunk, int Index, int Offset, int Format, int Width, int Height, int[] MipSizes)
{
    public const int Rgb565 = 8;

    public string FileName => $"tex_{Chunk:D3}_{Index}_{Width}x{Height}.png";
    public bool IsSupported => Format == Rgb565;

    public static List<Ntp3Texture> FindAll(XfbinFile x)
    {
        var list = new List<Ntp3Texture>();
        for (int c = 0; c < x.Chunks.Count; c++)
        {
            var d = x.Chunks[c].Data;
            if (d.Length < 0x30 || Encoding.ASCII.GetString(d, 12, 4) != "NTP3") continue;
            int count = U16(d, 18), p = 0x1c;
            for (int i = 0; i < count && p + 0x18 <= d.Length; i++)
            {
                int total = I32(d, p), dataSize = I32(d, p + 8), hs = U16(d, p + 12), mips = d[p + 0x11];
                int[] sizes = mips > 1 ? Enumerable.Range(0, mips).Select(m => I32(d, p + 0x30 + 4 * m)).ToArray() : [dataSize];
                list.Add(new Ntp3Texture(c, i, p + hs, d[p + 0x13], U16(d, p + 0x14), U16(d, p + 0x16), sizes));
                p += total;
            }
        }
        return list;
    }

    public byte[] DecodeRgb(XfbinFile x)
    {
        var d = x.Chunks[Chunk].Data;
        var rgb = new byte[Width * Height * 3];
        for (int i = 0; i < Width * Height; i++)
        {
            int v = U16(d, Offset + 2 * i);
            rgb[3 * i] = (byte)(((v >> 11) & 31) * 255 / 31);
            rgb[3 * i + 1] = (byte)(((v >> 5) & 63) * 255 / 63);
            rgb[3 * i + 2] = (byte)((v & 31) * 255 / 31);
        }
        return rgb;
    }

    public bool Replace(XfbinFile x, int w, int h, byte[] rgb)
    {
        if (w != Width || h != Height)
        {
            rgb = ImageResize.Area(w, h, rgb, Width, Height);
            (w, h) = (Width, Height);
        }
        if (DecodeRgb(x).AsSpan().SequenceEqual(rgb)) return false;
        var d = x.Chunks[Chunk].Data;
        int off = Offset;
        foreach (var size in MipSizes)
        {
            Array.Clear(d, off, size);
            for (int i = 0; i < w * h && 2 * i + 1 < size; i++)
            {
                int v = (rgb[3 * i] * 31 + 127) / 255 << 11 | (rgb[3 * i + 1] * 63 + 127) / 255 << 5 | (rgb[3 * i + 2] * 31 + 127) / 255;
                BinaryPrimitives.WriteUInt16BigEndian(d.AsSpan(off + 2 * i), (ushort)v);
            }
            off += size;
            (w, h, rgb) = HalfSize(w, h, rgb);
        }
        return true;
    }

    static (int, int, byte[]) HalfSize(int w, int h, byte[] rgb)
    {
        int nw = Math.Max(1, w / 2), nh = Math.Max(1, h / 2);
        var o = new byte[nw * nh * 3];
        for (int y = 0; y < nh; y++)
            for (int xx = 0; xx < nw; xx++)
                for (int c = 0; c < 3; c++)
                {
                    int sum = 0;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                            sum += rgb[(Math.Min(h - 1, 2 * y + dy) * w + Math.Min(w - 1, 2 * xx + dx)) * 3 + c];
                    o[(y * nw + xx) * 3 + c] = (byte)((sum + 2) / 4);
                }
        return (nw, nh, o);
    }

    static ushort U16(byte[] d, int o) => BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(o));
    static int I32(byte[] d, int o) => (int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o));
}
