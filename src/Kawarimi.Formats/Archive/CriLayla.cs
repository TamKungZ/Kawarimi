using System.Text;

namespace Kawarimi.Formats;

public static class CriLayla
{
    static readonly int[] Vle = { 2, 3, 5, 8 };

    public static byte[] Decompress(byte[] input)
    {
        if (input.Length < 16 || Encoding.ASCII.GetString(input, 0, 8) != "CRILAYLA") return input;
        int uncompSize = BitConverter.ToInt32(input, 8);
        if (uncompSize < 0 || uncompSize > 256 * 1024 * 1024) throw new InvalidDataException("Bad CRILAYLA size");
        var result = new byte[uncompSize + 0x100];
        int inputEnd = input.Length - 0x100 - 1;
        Array.Copy(input, inputEnd + 1, result, 0, 0x100);
        var bits = new BitReader(input, inputEnd);
        int outEnd = 0x100 + uncompSize - 1, produced = 0;
        while (produced < uncompSize)
        {
            if (bits.Read(1) == 0)
            {
                result[outEnd - produced++] = (byte)bits.Read(8);
                continue;
            }
            int offset = bits.Read(13) + 3, length = 3, lvl;
            for (lvl = 0; lvl < 4; lvl++)
            {
                int t = bits.Read(Vle[lvl]);
                length += t;
                if (t != (1 << Vle[lvl]) - 1) break;
            }
            if (lvl == 4)
            {
                int t;
                do
                {
                    t = bits.Read(8);
                    length += t;
                } while (t == 255);
            }
            for (int i = 0; i < length && produced < uncompSize; i++, produced++)
                result[outEnd - produced] = result[outEnd - produced + offset];
        }
        return result;
    }

    sealed class BitReader
    {
        readonly byte[] _input;
        int _pos, _bitsLeft;
        byte _pool;

        public BitReader(byte[] input, int start)
        {
            _input = input;
            _pos = start;
        }

        public int Read(int n)
        {
            int o = 0, got = 0;
            while (got < n)
            {
                if (_bitsLeft == 0)
                {
                    _pool = _input[_pos--];
                    _bitsLeft = 8;
                }
                int take = Math.Min(_bitsLeft, n - got);
                o = (o << take) | ((_pool >> (_bitsLeft - take)) & ((1 << take) - 1));
                _bitsLeft -= take;
                got += take;
            }
            return o;
        }
    }
}
