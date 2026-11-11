namespace Kawarimi.Tools.Cli;

internal static class DotEnv
{
    public static string? Read(string key)
    {
        foreach (var file in Files())
        {
            foreach (var line in File.ReadLines(file))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.TrimStart().StartsWith('#')) continue;
                if (line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return line[(eq + 1)..].Trim().Trim('"');
            }
        }
        return null;
    }

    static IEnumerable<string> Files()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
            {
                var f = Path.Combine(d.FullName, ".env");
                if (File.Exists(f)) yield return f;
            }
    }
}
