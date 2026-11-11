using System.Text;
using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class ArchiveCommands
{
    public static int Extract(string filter, string outDir)
    {
        string game = GameFolder.Find();
        var archives = new GameArchives(game, Ui.Warn);
        var files = archives.Find(filter);
        if (files.Count == 0)
        {
            Ui.Error($"No archive entry contains \"{filter}\".");
            return 1;
        }
        var report = new StringBuilder();
        int bad = 0;
        foreach (var f in files)
        {
            var plain = f.ReadPlain();
            bool ok = !f.Path.EndsWith(".xfbin", StringComparison.OrdinalIgnoreCase) || XfbinFile.HasMagic(plain);
            if (!ok) bad++;
            var dst = Path.Combine(outDir, "files", f.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.WriteAllBytes(dst, plain);
            string status = ok ? "ok" : "NOT-NUCC", source = archives.RelativePath(f.Archive);
            report.AppendLine($"{status}\t{f.Path}\t{plain.Length}\t{source}");
            Ui.Info($"  {status,-8} {f.Path} ({plain.Length} bytes) <- {source}");
        }
        File.WriteAllText(Path.Combine(outDir, "extract.txt"), report.ToString());
        Ui.Ok($"{files.Count - bad} file(s) extracted to {Path.Combine(outDir, "files")}" + (bad > 0 ? $", {bad} did not decode to NUCC" : ""));
        return bad == 0 ? 0 : 1;
    }

    public static int Encode(string input, string output)
    {
        var d = StormCipher.Encode(File.ReadAllBytes(input));
        File.WriteAllBytes(output, d);
        Ui.Ok($"Encrypted {d.Length} bytes -> {output}");
        return 0;
    }

    public static int Index(string filter)
    {
        var archives = new GameArchives(GameFolder.Find(), Ui.Warn);
        int n = 0;
        foreach (var f in archives.AllXfbin().Where(f => f.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            Ui.Info($"{f.Path}\t{f.Entry.ExtractSize}\t{archives.RelativePath(f.Archive)}");
            n++;
        }
        Ui.Info($"{n} entries");
        return 0;
    }
}
