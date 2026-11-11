using System.Diagnostics;
using System.Text;
using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Capture;

internal static class CaptureCommand
{
    const long OutputCap = 256L << 20;
    const int ChunkSize = 4 << 20;

    public static int Run(string gameDir, string outDir, int seconds)
    {
        var proc = Process.GetProcessesByName(Environment.GetEnvironmentVariable("KAWARIMI_PROCESS") ?? "NSUNS4").FirstOrDefault();
        if (proc == null)
        {
            Ui.Error("NSUNS4 is not running. Start the game, then run this again.");
            return 1;
        }
        Ui.Info("Indexing CPK archives...");
        var naming = new CaptureNaming(gameDir, new GameArchives(gameDir, Ui.Warn));
        using var mem = new ProcessMemory(proc);
        Directory.CreateDirectory(outDir);
        CharacterTableReader.Write(mem, outDir);

        var session = new Session(outDir, naming);
        var end = DateTime.UtcNow.AddSeconds(seconds);
        Ui.Info($"Capturing for {seconds}s. Play now: open character select, pick characters, start a fight.");
        while (DateTime.UtcNow < end && session.Total < OutputCap && !proc.HasExited)
        {
            foreach (var (addr, size, type) in mem.Regions())
            {
                if (type != ProcessMemory.MemPrivate || size < 4096 || size > (1L << 32)) continue;
                ScanRegion(mem, addr, size, session);
            }
            Thread.Sleep(100);
        }
        File.WriteAllText(Path.Combine(outDir, "capture.txt"), session.Report.ToString());
        Ui.Ok($"Done: {session.Named} named, {session.Unnamed} unnamed, {session.Total >> 20} MB -> {outDir}");
        return 0;
    }

    public static int NameDir(string gameDir, string inDir, string outDir)
    {
        var session = new Session(outDir, new CaptureNaming(gameDir, new GameArchives(gameDir, Ui.Warn)));
        int bad = 0;
        foreach (var f in Directory.GetFiles(inDir, "*.xfbin"))
        {
            var data = File.ReadAllBytes(f);
            if (!XfbinFile.TryMeasure(data, out int len, out var x) || x == null) bad++;
            else session.Save(x, data.AsSpan(0, len).ToArray());
        }
        Ui.Ok($"named {session.Named}, unnamed {session.Unnamed}, not measurable {bad} -> {outDir}");
        return 0;
    }

    static void ScanRegion(ProcessMemory mem, nint addr, long size, Session session)
    {
        var magic = "NUCC"u8.ToArray();
        for (long off = 0; off < size && session.Total < OutputCap; off += ChunkSize - 8)
        {
            int n = (int)Math.Min(ChunkSize, size - off);
            var buf = mem.Read((nint)(addr + off), n);
            if (buf == null) break;
            for (int i = buf.AsSpan().IndexOf(magic); i >= 0 && i < n;)
            {
                long at = addr + off + i;
                var blob = mem.Read((nint)at, (int)Math.Min(64 << 20, size - off - i)) ?? buf.AsSpan(i).ToArray();
                if (XfbinFile.TryMeasure(blob, out int flen, out var x) && x != null && flen >= 64) session.Save(x, blob.AsSpan(0, flen).ToArray());
                int next = buf.AsSpan(i + 4).IndexOf(magic);
                i = next < 0 ? -1 : i + 4 + next;
            }
        }
    }

    sealed class Session
    {
        readonly string _outDir;
        readonly CaptureNaming _naming;
        readonly HashSet<string> _seen = new();

        public Session(string outDir, CaptureNaming naming)
        {
            _outDir = outDir;
            _naming = naming;
        }

        public StringBuilder Report { get; } = new();
        public long Total { get; private set; }
        public int Named { get; private set; }
        public int Unnamed { get; private set; }

        public void Save(XfbinFile x, byte[] file)
        {
            var h = CaptureNaming.Hash(file);
            if (!_seen.Add(h)) return;
            var (path, exact, cands) = _naming.Name(x, file);
            string rel = path != null
                ? Path.Combine("files", path.Replace('/', Path.DirectorySeparatorChar))
                : Path.Combine("unnamed", $"{file.Length}_{h}.xfbin");
            var dest = Path.Combine(_outDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllBytes(dest, file);
            Total += file.Length;
            if (path != null) Named++;
            else Unnamed++;
            Report.AppendLine($"{rel}\t{file.Length}\t{(path != null ? (exact ? "exact" : "size-match") : "candidates: " + string.Join(", ", cands))}");
            Ui.Info($"  {path ?? $"(unnamed, {cands.Count} candidates)"}  {file.Length} bytes");
        }
    }
}
