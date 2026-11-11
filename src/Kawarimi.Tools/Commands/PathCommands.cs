using System.Runtime.InteropServices;
using Kawarimi.Tools.Cli;

namespace Kawarimi.Tools.Commands;

internal static class PathCommands
{
    const string Variable = "PATH";

    public static int Add()
    {
        string dir = Folder();
        var parts = Parts();
        if (parts.Any(p => Same(p, dir)))
        {
            Ui.Info($"Already on PATH: {dir}");
            return 0;
        }
        Save(parts.Append(dir));
        Ui.Ok($"Added to your PATH: {dir}");
        Ui.Info("Open a new terminal, then run  Kawarimi build <Name>  from any folder.");
        return 0;
    }

    public static int Remove()
    {
        string dir = Folder();
        var parts = Parts();
        if (!parts.Any(p => Same(p, dir)))
        {
            Ui.Info($"Not on PATH: {dir}");
            return 0;
        }
        Save(parts.Where(p => !Same(p, dir)));
        Ui.Ok($"Removed from your PATH: {dir}");
        return 0;
    }

    static string Folder() => Path.GetFullPath(AppContext.BaseDirectory).TrimEnd('\\', '/');

    static List<string> Parts() =>
        (Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User) ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

    static bool Same(string a, string b) =>
        string.Equals(a.TrimEnd('\\', '/'), b, StringComparison.OrdinalIgnoreCase);

    static void Save(IEnumerable<string> parts)
    {
        Environment.SetEnvironmentVariable(Variable, string.Join(';', parts), EnvironmentVariableTarget.User);
        SendMessageTimeout(new IntPtr(0xffff), 0x1a, IntPtr.Zero, "Environment", 2, 2000, out _);
    }

    [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
