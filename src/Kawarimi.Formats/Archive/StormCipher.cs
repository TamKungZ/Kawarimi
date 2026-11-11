using System.Text;

namespace Kawarimi.Formats;

public static class StormCipher
{
    public static readonly byte[] EncryptedSignature = [0x8f, 0xcf, 0x31, 0x40, 0x76, 0x41, 0x95, 0xd1];

    public static void Apply(Span<byte> d)
    {
        uint x = 0x154f048d, y = 0x02b3d1cd, z = 0x43a3d2f9, w = 0x617e9602;
        for (int i = 0; i < d.Length; i += 4)
        {
            uint t = x ^ (x << 11);
            t ^= t >> 8;
            uint next = w ^ (w >> 19) ^ t;
            for (int j = 0; j < 4 && i + j < d.Length; j++) d[i + j] ^= (byte)(next >> (8 * j));
            x = y;
            y = z;
            z = w;
            w = next;
        }
    }

    public static bool IsPlain(ReadOnlySpan<byte> d) =>
        XfbinFile.HasMagic(d) || (d.Length >= 8 && Encoding.ASCII.GetString(d[..8]) == "CRILAYLA");

    public static byte[] Decode(byte[] raw)
    {
        if (!IsPlain(raw)) Apply(raw);
        return CriLayla.Decompress(raw);
    }

    public static byte[] Encode(byte[] plain)
    {
        if (!XfbinFile.HasMagic(plain)) throw new InvalidDataException("not a plain xfbin (NUCC)");
        var d = (byte[])plain.Clone();
        Apply(d);
        return d;
    }
}
