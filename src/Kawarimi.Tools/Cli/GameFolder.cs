namespace Kawarimi.Tools.Cli;

internal static class GameFolder
{
    public const string GameExe = "NSUNS4.exe";
    public const string EnvVariable = "KAWARIMI_GAME_DIR";

    public static string Find()
    {
        foreach (var candidate in Candidates())
            if (!string.IsNullOrEmpty(candidate) && File.Exists(Path.Combine(candidate, GameExe))) return Path.GetFullPath(candidate);
        throw new InvalidOperationException($"{GameExe} not found. Run Kawarimi from inside the game folder, or set {EnvVariable} in .env.");
    }

    public static string ModsFolder(string gameDir) => Path.Combine(gameDir, "mods");

    public static string ProjectsFolder(string gameDir) => Path.Combine(gameDir, "projects");

    public static string ShareFolder(string gameDir) => Path.Combine(gameDir, "share");

    static IEnumerable<string?> Candidates()
    {
        yield return Environment.GetEnvironmentVariable(EnvVariable);
        yield return DotEnv.Read(EnvVariable);
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                yield return d.FullName;
    }
}
