using System.Text;

namespace Kawarimi.Host;

public enum ModStatus { Loaded, Failed, Skipped }

public sealed class ModReportEntry
{
    public string Folder { get; init; } = "";
    public string Id { get; set; } = "";
    public ModStatus Status { get; set; }
    public int Skins { get; set; }
    public List<string> Messages { get; } = new();
}

public static class ModReport
{
    public static string Format(IReadOnlyList<ModReportEntry> report, int registered)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Kawarimi mod report - {report.Count} mod folder(s), {registered} skin(s) registered");
        foreach (var r in report)
        {
            sb.AppendLine($"[{r.Status}] {r.Folder} ({r.Skins} skins)");
            foreach (var m in r.Messages) sb.AppendLine($"    - {m}");
        }
        return sb.ToString();
    }
}
