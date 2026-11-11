using System.Text;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Capture;

internal static class CharacterTableReader
{
    const long ImageBase = 0x140000000, TableGlobal = 0x14161C890;
    const int RecordSize = 0x2d8;

    public static int Write(ProcessMemory mem, string outDir)
    {
        long mgr = mem.ReadI64((nint)(mem.Base + (TableGlobal - ImageBase)));
        long tbl = mgr == 0 ? 0 : mem.ReadI64((nint)(mgr + 0xf8));
        int count = tbl == 0 ? 0 : mem.ReadI32((nint)(tbl + 0x18));
        long arr = tbl == 0 ? 0 : mem.ReadI64((nint)(tbl + 0x28)), codes = tbl == 0 ? 0 : mem.ReadI64((nint)(tbl + 0x20));
        if (count <= 0 || count > 4096 || arr == 0)
        {
            Ui.Warn("character table not loaded yet (wait for the main menu); skipped");
            return 1;
        }
        var data = mem.Read((nint)arr, count * RecordSize);
        var code = codes == 0 ? null : mem.Read((nint)codes, count * 8);
        if (data == null) return 1;
        File.WriteAllBytes(Path.Combine(outDir, "characters.records.bin"), data);
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            string c = code == null ? "?" : SlotText(code.AsSpan(i * 8, 8)) ?? "?";
            sb.Append($"#{i} code={c} costumes:");
            var r = data.AsSpan(i * RecordSize, RecordSize);
            for (int s = 1; s < RecordSize / 8; s++)
                if (SlotText(r.Slice(s * 8, 8)) is { } txt) sb.Append($" [{s}]{txt}");
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(outDir, "characters.txt"), sb.ToString());
        Ui.Info($"  {count} characters -> characters.txt");
        return 0;
    }

    static string? SlotText(ReadOnlySpan<byte> s)
    {
        int n = 0;
        while (n < 8 && s[n] != 0)
        {
            if (s[n] < 0x21 || s[n] > 0x7e) return null;
            n++;
        }
        if (n < 3 || n > 7) return null;
        for (int i = n; i < 8; i++)
            if (s[i] != 0) return null;
        return Encoding.ASCII.GetString(s[..n]);
    }
}
