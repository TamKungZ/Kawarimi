using System.Diagnostics;
using Kawarimi.Formats;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class ModCompiler
{
    public static bool HasSdk()
    {
        try
        {
            var (code, output) = Run("dotnet", "--list-sdks", Directory.GetCurrentDirectory());
            return code == 0 && output.Split('\n').Any(l => int.TryParse(l.Split('.')[0], out int major) && major >= 8);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool Compile(string projectDir, ModProjectInfo info, string modDir)
    {
        string csproj = Path.Combine(projectDir, info.Name + ".csproj"), output = Path.Combine(projectDir, "bin", "kawarimi");
        var (code, log) = Run("dotnet", $"build \"{csproj}\" -c Release -o \"{output}\" --nologo -v q -p:KawarimiSkipMod=true", projectDir);
        if (code != 0)
        {
            Ui.Error("C# build failed:");
            foreach (var line in log.Split('\n').Where(l => l.Contains("error", StringComparison.OrdinalIgnoreCase)).Distinct().Take(20))
                Ui.Error("  " + line.Trim());
            return false;
        }
        File.Copy(Path.Combine(output, info.Name + ".dll"), Path.Combine(modDir, info.Name + ".dll"), overwrite: true);
        return true;
    }

    static (int code, string output) Run(string exe, string args, string dir)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        string text = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode, text);
    }
}
